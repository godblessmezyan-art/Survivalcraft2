using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Home
{
    public sealed partial class SubsystemHomeFurniture
    {
        private sealed class SeatState
        {
            internal ComponentPlayer Player;
            internal Point3 Root;
            internal int Value;
            internal byte Seat;
            internal uint Revision;
            internal Vector3 Entry, OriginalBox, AppliedBox;
            internal double Lease, NextCheck, NextPublish;
            internal bool Applied;
        }
        private struct SeatRequest { internal ComponentPlayer Player; internal HomeSeatingPackage Packet; internal Client Reply; }
        private sealed class SeatPeer
        {
            internal ComponentPlayer Player;
            internal Client Client;
            internal uint Sequence;
            internal bool HasSequence;
            internal double NextIntent, NextSnapshot;
        }
        private readonly Queue<SeatRequest> seatRequests = new Queue<SeatRequest>();
        private readonly Dictionary<ushort, SeatState> seats = new Dictionary<ushort, SeatState>();
        private readonly Dictionary<ushort, uint> seatVersions = new Dictionary<ushort, uint>();
        private readonly Dictionary<ushort, SeatPeer> seatPeers = new Dictionary<ushort, SeatPeer>();
        private readonly List<ushort> endedSeats = new List<ushort>();
        private readonly Dictionary<ComponentPlayer, HomeSeatHud> seatHuds = new Dictionary<ComponentPlayer, HomeSeatHud>();
        private readonly DynamicArray<ComponentBody> nearSeatBodies = new DynamicArray<ComponentBody>();
        private uint nextSeatRevision, seatRequestSequence;
        private double nextSeatSnapshot, nextSeatLease;
        private double nextSeatIntent;

        internal void QueueSeat(ComponentPlayer player, HomeSeatingPackage packet, Client reply)
        { lock (seatRequests) if (seatRequests.Count < 128) seatRequests.Enqueue(new SeatRequest { Player = player, Packet = packet, Reply = reply }); }
        private ComponentPlayer SeatPlayer(ushort id)
        {
            foreach (var p in Project.FindSubsystem<SubsystemPlayers>(true).ComponentPlayers) if (p.Entity.EntityId == id && p.IsAddedToProject) return p;
            return null;
        }
        private bool MaySit(ComponentPlayer p) => p != null && p.IsAddedToProject && p.PlayerData.IsReadyForPlaying && p.ComponentHealth.Health > 0
            && p.ComponentSleep.SleepFactor <= 0 && p.ComponentRider.Mount == null && p.ComponentBody.ParentBody == null
            && p.ComponentBody.ImmersionFactor <= 0 && p.ComponentBody.CrouchFactor < .1f && !p.ComponentLocomotion.IsCreativeFlyEnabled
            && !p.ComponentLocomotion.LadderValue.HasValue && HomeSeatRules.Finite(p.ComponentBody.Position) && p.ComponentCreatureModel is ComponentHumanModel;
        private void RequestSeat(ComponentPlayer player, Point3 root, int value)
        {
            SeatState existing;
            if (seats.TryGetValue(player.Entity.EntityId, out existing)) { RequestStand(player); return; }
            byte preferred = 0;
            if (HomeSeatRules.Count(Terrain.ExtractContents(value)) == 2
                && Vector3.DistanceSquared(player.ComponentBody.Position, HomeSeatRules.Position(root, value, 1)) < Vector3.DistanceSquared(player.ComponentBody.Position, HomeSeatRules.Position(root, value, 0))) preferred = 1;
            SendSeatIntent(player, new HomeSeatingPackage { Kind = 0, Root = root, Expected = Terrain.ReplaceLight(value, 0), Seat = preferred });
        }
        private void SendSeatIntent(ComponentPlayer player, HomeSeatingPackage q)
        {
            if (q.Kind == 0 || q.Kind == 1) { if (clock.GameTime < nextSeatIntent) return; nextSeatIntent = clock.GameTime + .25; }
            q.Actor = player.Entity.EntityId; if (unchecked(++seatRequestSequence) == 0) ++seatRequestSequence; q.Revision = seatRequestSequence;
            if (CommonLib.WorkType == WorkType.Client) CommonLib.Net.QueuePackage(q);
            else QueueSeat(player, q, null);
        }
        internal bool HasSeat(ComponentPlayer p) => p != null && seats.ContainsKey(p.Entity.EntityId);
        internal void RequestStand(ComponentPlayer p) { if (HasSeat(p)) SendSeatIntent(p, new HomeSeatingPackage { Kind = 1 }); }
        private void SeatMessage(ComponentPlayer p, string text)
        {
            if (CommonLib.WorkType == WorkType.Local || p.PlayerData.IsMainPlayer) p.ComponentGui.DisplaySmallMessage(text, Color.White, false, false);
            else if (CommonLib.WorkType == WorkType.Server && p.PlayerData.Client != null)
                CommonLib.Net.QueuePackage(new HomeSeatingPackage { Kind = 5, Actor = p.Entity.EntityId, Seat = (byte)(text == "周围没有空位" ? 2 : 1), To = p.PlayerData.Client });
        }
        private HomeSeatingPackage SeatPacket(SeatState s, bool active, bool move, Vector3 exit)
            => new HomeSeatingPackage { Kind = 2, Actor = s.Player.Entity.EntityId, Root = s.Root, Expected = s.Value, Seat = s.Seat,
                Active = active, MoveOnExit = move, Exit = exit, Revision = s.Revision };
        private void PublishSeat(SeatState s, Client to = null)
        { if (CommonLib.WorkType == WorkType.Server) CommonLib.Net.QueuePackage(new HomeSeatingPackage { Kind = 2, Actor = s.Player.Entity.EntityId, Root = s.Root, Expected = s.Value, Seat = s.Seat, Active = true, Revision = s.Revision, To = to }); }
        private bool SeatOccupied(Point3 root, int index)
        { foreach (var s in seats.Values) if (s.Root.X == root.X && s.Root.Y == root.Y && s.Root.Z == root.Z && s.Seat == index) return true; return false; }
        private bool SeatPermission(ComponentPlayer p, Point3 root)
        {
            Territoriy region;
            return !SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(root.X, root.Z, out region)
                || (region.AllowBlockBehavior && (!region.IsVisible || !SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(region, root.X, root.Z)))
                || SubsystemBedrockBlockBehavior.AllowPlayerAction(p, region);
        }
        private void StartSeat(ComponentPlayer p, HomeSeatingPackage q)
        {
            if (!MaySit(p) || seats.ContainsKey(p.Entity.EntityId) || seats.Count >= HomeSeatRules.MaxSeats || !HomeSeatRules.Valid(q.Root) || !Ready(q.Root)) return;
            int actual = Signature(q.Root), id = Terrain.ExtractContents(actual);
            if (!HomeSeatRules.IsSeat(id) || actual != q.Expected || Terrain.ExtractData(actual) != HomeRules.Normalize(id, Terrain.ExtractData(actual)) || q.Seat >= HomeSeatRules.Count(id) || !SeatPermission(p, q.Root)) return;
            var box = p.ComponentBody.BoxSize;
            if (!HomeSeatRules.Finite(box) || box.X <= 0 || box.Z <= 0 || box.Y < .5f || box.Y > 3 || box.X > 1 || box.Z > 1) return;
            for (int n = 0; n < HomeSeatRules.Count(id); n++)
            {
                byte index = (byte)((q.Seat + n) % HomeSeatRules.Count(id));
                Vector3 at = HomeSeatRules.Position(q.Root, actual, index);
                if (SeatOccupied(q.Root, index) || Vector3.DistanceSquared(p.ComponentBody.Position, at) > 9 || !RoomFor(p, at, HomeSeatRules.SeatedBox(box))) continue;
                var eye = p.ComponentCreatureModel.EyePosition;
                var sight = SubsystemTerrain.Raycast(eye, at + Vector3.UnitY * .7f, false, true, (v, d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
                if (sight.HasValue && !InteractionBelongsTo(q.Root, id, sight.Value.CellFace.Point)) continue;
                if (unchecked(++nextSeatRevision) == 0) ++nextSeatRevision;
                var s = new SeatState { Player = p, Root = q.Root, Value = actual, Seat = index, Revision = nextSeatRevision, Entry = p.ComponentBody.Position, OriginalBox = box, AppliedBox = HomeSeatRules.SeatedBox(box), Lease = clock.GameTime + 5 };
                seats.Add(p.Entity.EntityId, s); seatVersions[p.Entity.EntityId] = s.Revision; ApplySeat(s, true); PublishSeat(s); return;
            }
            SeatMessage(p, "座位暂不可用");
        }
        private bool RoomFor(ComponentPlayer player, Vector3 at, Vector3 size)
        {
            if (!HomeSeatRules.Finite(at) || at.Y < 1 || at.Y + size.Y >= 255 || Math.Abs(at.X) > 1048576 || Math.Abs(at.Z) > 1048576) return false;
            var bounds = new BoundingBox(at - new Vector3(size.X / 2, 0, size.Z / 2) + new Vector3(.006f), at + new Vector3(size.X / 2, size.Y, size.Z / 2) - new Vector3(.006f));
            int x0 = Terrain.ToCell(bounds.Min.X), x1 = Terrain.ToCell(bounds.Max.X), y0 = Terrain.ToCell(bounds.Min.Y), y1 = Terrain.ToCell(bounds.Max.Y), z0 = Terrain.ToCell(bounds.Min.Z), z1 = Terrain.ToCell(bounds.Max.Z);
            if ((long)(x1-x0+1)*(y1-y0+1)*(z1-z0+1) > 96) return false;
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
            {
                var chunk = SubsystemTerrain.Terrain.GetChunkAtCell(x, z); if (chunk == null || chunk.State < TerrainChunkState.Valid) return false;
                for (int y = y0; y <= y1; y++)
                {
                    int value = SubsystemTerrain.Terrain.GetCellValue(x,y,z); var block = BlocksManager.Blocks[Terrain.ExtractContents(value)]; if (block == null || !block.IsCollidable) continue;
                    var boxes = block.GetCustomCollisionBoxes(SubsystemTerrain,value);
                    if (boxes == null) boxes = new[] { new BoundingBox(Vector3.Zero,Vector3.One) };
                    foreach (var b in boxes) if (bounds.Intersection(new BoundingBox(b.Min+new Vector3(x,y,z),b.Max+new Vector3(x,y,z)))) return false;
                }
            }
            nearSeatBodies.Clear(); Project.FindSubsystem<SubsystemBodies>(true).FindBodiesAroundPoint(at.XZ, 2, nearSeatBodies);
            for (int i = 0; i < nearSeatBodies.Count; i++) if (nearSeatBodies.Array[i] != player.ComponentBody && bounds.Intersection(nearSeatBodies.Array[i].BoundingBox)) return false;
            return true;
        }
        private bool ExitAt(SeatState s, Vector3 candidate, out Vector3 exit)
        {
            exit = candidate; Vector3 start = candidate + Vector3.UnitY * 1.1f;
            var floor = SubsystemTerrain.Raycast(start, candidate - Vector3.UnitY * 1.5f, false, true, (v,d) => BlocksManager.Blocks[Terrain.ExtractContents(v)].IsCollidable);
            if (!floor.HasValue) return false;
            exit.Y = start.Y - floor.Value.Distance + .025f;
            return RoomFor(s.Player, exit, s.OriginalBox);
        }
        private bool FindSeatExit(SeatState s, out Vector3 exit)
        {
            int data = Terrain.ExtractData(s.Value); bool dining = Terrain.ExtractContents(s.Value) == HomeRules.Dining;
            Vector3[] candidates = dining ? new[] { new Vector3(s.Seat == 0 ? -.6f : 2.6f,0,1),new Vector3(.5f,0,-.6f),new Vector3(.5f,0,2.6f),new Vector3(1,0,1) }
                : new[] { new Vector3(.5f,0,-.6f),new Vector3(-.6f,0,.4f),new Vector3(1.6f,0,.4f),new Vector3(.5f,.6f,.42f) };
            foreach (Vector3 local in candidates) if (ExitAt(s, HomeSeatRules.Transform(s.Root,data,local),out exit)) return true;
            if (Vector3.DistanceSquared(s.Entry, HomeSeatRules.Position(s.Root,s.Value,s.Seat)) < 16 && ExitAt(s,s.Entry,out exit)) return true;
            exit = s.Player.ComponentBody.Position; return false;
        }
        private void RestoreSeat(SeatState s)
        {
            if (s.Player?.ComponentBody != null && s.Applied && s.Player.ComponentBody.BoxSize == s.AppliedBox) s.Player.ComponentBody.BoxSize = s.OriginalBox;
            if (s.Player?.ComponentCreatureModel != null) { s.Player.ComponentCreatureModel.m_eyePosition = null; s.Player.ComponentCreatureModel.m_eyeRotation = null; }
            s.Applied = false;
        }
        private bool EndSeat(ushort actor, bool forced, bool relocate)
        {
            SeatState s; if (!seats.TryGetValue(actor,out s)) return true;
            Vector3 exit = s.Player.ComponentBody.Position; bool move = relocate && FindSeatExit(s,out exit);
            if (!forced && !move) { SeatMessage(s.Player,"周围没有空位"); return false; }
            seats.Remove(actor); RestoreSeat(s);
            if (move) MoveFromSeat(s.Player, exit);
            if (unchecked(++nextSeatRevision) == 0) ++nextSeatRevision; s.Revision = nextSeatRevision; seatVersions[actor] = s.Revision;
            if (CommonLib.WorkType == WorkType.Server) CommonLib.Net.QueuePackage(SeatPacket(s,false,move,exit));
            return true;
        }
        private static void MoveFromSeat(ComponentPlayer player, Vector3 exit)
        {
            var body = player.ComponentBody;
            body.Position = exit; body.Velocity = Vector3.Zero;
            body.netPosition.SetNext(exit); body.netRotation.SetNext(body.Rotation); body.netVelocity.SetNext(Vector3.Zero);
        }
        private void ApplySeat(SeatState s, bool position)
        {
            var p = s.Player; if (p == null || !p.IsAddedToProject) return;
            var body = p.ComponentBody;
            bool fresh = !s.Applied;
            if (fresh) { s.OriginalBox = body.BoxSize; s.AppliedBox = HomeSeatRules.SeatedBox(s.OriginalBox); s.Applied = true; }
            body.BoxSize = s.AppliedBox;
            if (position)
            {
                body.Position = HomeSeatRules.Position(s.Root,s.Value,s.Seat);
                if (fresh) body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,HomeSeatRules.Yaw(s.Value,s.Seat));
                body.Velocity = Vector3.Zero;
                body.netPosition.SetNext(body.Position); body.netRotation.SetNext(body.Rotation); body.netVelocity.SetNext(Vector3.Zero);
            }
            var human = p.ComponentCreatureModel as ComponentHumanModel;
            if (human != null) human.m_eyePosition = human.CalculateEyePosition() - Vector3.UnitY * HomeSeatRules.VisualDrop(s.OriginalBox);
        }
        private void ReceiveSeatState(HomeSeatingPackage q)
        {
            uint old; if (seatVersions.TryGetValue(q.Actor,out old) && q.Revision != old && !HomeSeatRules.Newer(q.Revision,old)) return;
            if (!HomeSeatRules.Valid(q.Root) || !HomeSeatRules.IsSeat(Terrain.ExtractContents(q.Expected)) || q.Seat >= HomeSeatRules.Count(Terrain.ExtractContents(q.Expected))) return;
            var player = SeatPlayer(q.Actor); if (player == null) return;
            SeatState previous; seats.TryGetValue(q.Actor,out previous);
            if (!q.Active)
            {
                if (previous != null) { seats.Remove(q.Actor); RestoreSeat(previous); }
                seatVersions[q.Actor] = q.Revision;
                if (q.MoveOnExit && HomeSeatRules.Finite(q.Exit) && Vector3.DistanceSquared(player.ComponentBody.Position,HomeSeatRules.Position(q.Root,q.Expected,q.Seat)) < 16)
                    MoveFromSeat(player, q.Exit);
                return;
            }
            if (previous != null && previous.Revision == q.Revision) { previous.Lease = clock.GameTime + 5; return; }
            if (previous != null) RestoreSeat(previous);
            if (seats.Count >= HomeSeatRules.MaxSeats && previous == null) return;
            if (seatVersions.Count >= 128) seatVersions.Clear(); seatVersions[q.Actor] = q.Revision;
            var s = new SeatState { Player=player,Root=q.Root,Value=q.Expected,Seat=q.Seat,Revision=q.Revision,Entry=player.ComponentBody.Position,Lease=clock.GameTime+5 };
            seats[q.Actor]=s;
            if (player.PlayerData.IsMainPlayer) ApplySeat(s,true);
        }
        private void UpdateSeats(float dt)
        {
            for (int n=0;n<8;n++)
            {
                SeatRequest r; lock(seatRequests) { if(seatRequests.Count==0)break;r=seatRequests.Dequeue(); }
                var q=r.Packet;
                if (CommonLib.WorkType == WorkType.Client)
                {
                    if(q.Kind==2)ReceiveSeatState(q);
                    else if(q.Kind==5&&CommonLib.MainPlayer?.Entity.EntityId==q.Actor)
                        CommonLib.MainPlayer.ComponentGui.DisplaySmallMessage(q.Seat==2?"周围没有空位":"座位暂不可用",Color.White,false,false);
                    continue;
                }
                var p=r.Player; if(p==null||!p.IsAddedToProject)continue;
                SeatPeer peer;
                if(!seatPeers.TryGetValue(q.Actor,out peer)||!ReferenceEquals(peer.Player,p)||!ReferenceEquals(peer.Client,r.Reply))
                {
                    // Entity IDs can be reused on reconnect; sequence numbers belong to the authenticated session.
                    if(peer!=null)EndSeat(q.Actor,true,false);
                    if(seatPeers.Count>=128&&!seatPeers.ContainsKey(q.Actor))
                    {
                        endedSeats.Clear();foreach(var entry in seatPeers)if(!entry.Value.Player.IsAddedToProject)endedSeats.Add(entry.Key);
                        foreach(ushort actor in endedSeats)seatPeers.Remove(actor);
                        if(seatPeers.Count>=128)continue;
                    }
                    peer=new SeatPeer{Player=p,Client=r.Reply};seatPeers[q.Actor]=peer;
                }
                if(peer.HasSequence&&!HomeSeatRules.Newer(q.Revision,peer.Sequence))continue;
                peer.HasSequence=true;peer.Sequence=q.Revision;
                SeatState s;
                if(q.Kind==4) { if(seats.TryGetValue(q.Actor,out s)&&s.Root.X==q.Root.X&&s.Root.Y==q.Root.Y&&s.Root.Z==q.Root.Z&&s.Value==q.Expected&&s.Seat==q.Seat)s.Lease=clock.GameTime+5;continue; }
                if(clock.GameTime<peer.NextIntent)continue;peer.NextIntent=clock.GameTime+.2;
                if(q.Kind==0)StartSeat(p,q);
                else if(q.Kind==1)EndSeat(q.Actor,false,true);
                else if(q.Kind==3)
                {
                    if(clock.GameTime<peer.NextSnapshot)continue;peer.NextSnapshot=clock.GameTime+2;
                    foreach(var occupied in seats.Values)PublishSeat(occupied,r.Reply);
                }
            }
            endedSeats.Clear();
            foreach(var pair in seats)
            {
                var s=pair.Value; var p=s.Player;
                bool valid=p!=null&&p.IsAddedToProject&&p.ComponentHealth.Health>0&&p.ComponentSleep.SleepFactor<=0&&p.ComponentRider.Mount==null&&p.ComponentBody.ParentBody==null
                    && p.ComponentBody.ImmersionFactor<=0 && !p.ComponentLocomotion.IsCreativeFlyEnabled
                    && Vector3.DistanceSquared(p.ComponentBody.Position,HomeSeatRules.Position(s.Root,s.Value,s.Seat))<(CommonLib.WorkType==WorkType.Client&&!p.PlayerData.IsMainPlayer?16:4);
                if(!valid){endedSeats.Add(pair.Key);continue;}
                if(s.Applied&&p.ComponentBody.BoxSize!=s.AppliedBox)valid=false;
                if(p!=null&&p.ComponentHealth.HealthChange<0)valid=false;
                if(CommonLib.WorkType==WorkType.Client) { if(clock.GameTime>s.Lease)valid=false; }
                else
                {
                    if(!p.PlayerData.IsMainPlayer&&clock.GameTime>s.Lease)valid=false;
                    if(clock.GameTime>=s.NextCheck)
                    { s.NextCheck=clock.GameTime+.5; if(!Ready(s.Root)||Signature(s.Root)!=s.Value||!SeatPermission(p,s.Root)||!RoomFor(p,HomeSeatRules.Position(s.Root,s.Value,s.Seat),s.AppliedBox))valid=false; }
                    if(clock.GameTime>=s.NextPublish){s.NextPublish=clock.GameTime+1.25;PublishSeat(s);}
                }
                if(!valid)endedSeats.Add(pair.Key);
            }
            foreach(ushort actor in endedSeats)
            {
                if(CommonLib.WorkType==WorkType.Client){SeatState s=seats[actor];RestoreSeat(s);seats.Remove(actor);}
                else
                {
                    var s=seats[actor];var p=s.Player;
                    bool nearby=p!=null&&p.IsAddedToProject&&p.ComponentHealth.Health>0
                        && Vector3.DistanceSquared(p.ComponentBody.Position,HomeSeatRules.Position(s.Root,s.Value,s.Seat))<4;
                    EndSeat(actor,true,nearby);
                }
            }
            if(CommonLib.WorkType==WorkType.Client&&CommonLib.MainPlayer!=null&&CommonLib.MainPlayer.IsAddedToProject)
            {
                var p=CommonLib.MainPlayer;
                if(clock.GameTime>=nextSeatSnapshot){nextSeatSnapshot=clock.GameTime+3;SendSeatIntent(p,new HomeSeatingPackage{Kind=3});}
                SeatState s;if(clock.GameTime>=nextSeatLease&&seats.TryGetValue(p.Entity.EntityId,out s))
                {nextSeatLease=clock.GameTime+1;SendSeatIntent(p,new HomeSeatingPackage{Kind=4,Root=s.Root,Expected=s.Value,Seat=s.Seat});}
            }
        }
        internal void AfterSeatUpdates()
        {
            foreach(var s in seats.Values)if(s.Player.IsAddedToProject)ApplySeat(s,CommonLib.WorkType!=WorkType.Client||s.Player.PlayerData.IsMainPlayer);
        }
        internal bool HandleSeatInput(ComponentPlayer p)
        {
            if(!HasSeat(p))return false;
            var input=p.ComponentInput.m_playerInput;
            bool leave=input.Move.LengthSquared()>.0225f||input.SneakMove.LengthSquared()>.0225f||input.Jump||input.ToggleMount||input.ToggleSneak||input.Interact.HasValue||input.Hit.HasValue||input.Dig.HasValue||input.Aim.HasValue;
            if(leave)RequestStand(p);
            p.ComponentInput.m_playerInput.Move=Vector3.Zero;p.ComponentInput.m_playerInput.SneakMove=Vector3.Zero;p.ComponentInput.m_playerInput.Jump=false;
            p.ComponentInput.m_playerInput.Hit=null;p.ComponentInput.m_playerInput.Dig=null;p.ComponentInput.m_playerInput.Aim=null;p.ComponentInput.m_playerInput.Interact=null;
            p.ComponentInput.m_playerInput.ToggleMount=false;
            p.ComponentInput.m_playerInput.ToggleSneak=false;
            return true;
        }
        internal bool AnimateSeat(ComponentCreatureModel model)
        {
            var human=model as ComponentHumanModel;var player=model.Entity.FindComponent<ComponentPlayer>(false);SeatState s;
            if(human==null||player==null||!seats.TryGetValue(player.Entity.EntityId,out s)||!player.IsAddedToProject||player.ComponentHealth.Health<=0||human.Model==null)return false;
            if(human.m_bodyBone==null||human.m_headBone==null||human.m_leg1Bone==null||human.m_leg2Bone==null||human.m_hand1Bone==null||human.m_hand2Bone==null)return false;
            var at=player.ComponentBody.Position-Vector3.UnitY*HomeSeatRules.VisualDrop(s.OriginalBox);
            human.SetBoneTransform(human.m_bodyBone.Index,Matrix.CreateRotationY(HomeSeatRules.Yaw(s.Value,s.Seat))*Matrix.CreateTranslation(at));
            var look=player.ComponentLocomotion.LookAngles;
            float headYaw=MathUtils.NormalizeAngle(player.ComponentBody.Rotation.ToYawPitchRoll().X-HomeSeatRules.Yaw(s.Value,s.Seat)-look.X);
            human.SetBoneTransform(human.m_headBone.Index,Matrix.CreateRotationX(MathUtils.Clamp(look.Y,-.65f,.65f))*Matrix.CreateRotationZ(MathUtils.Clamp(headYaw,-1.2f,1.2f)));
            human.SetBoneTransform(human.m_leg1Bone.Index,Matrix.CreateRotationY(.10f)*Matrix.CreateRotationX(1.10f));
            human.SetBoneTransform(human.m_leg2Bone.Index,Matrix.CreateRotationY(-.10f)*Matrix.CreateRotationX(1.10f));
            human.SetBoneTransform(human.m_hand1Bone.Index,Matrix.CreateRotationY(.12f)*Matrix.CreateRotationX(.35f));
            human.SetBoneTransform(human.m_hand2Bone.Index,Matrix.CreateRotationY(-.12f)*Matrix.CreateRotationX(.35f));
            return true;
        }
        private void ReleaseFurnitureSeats(Point3 root)
        {
            endedSeats.Clear();foreach(var pair in seats)if(pair.Value.Root.X==root.X&&pair.Value.Root.Y==root.Y&&pair.Value.Root.Z==root.Z)endedSeats.Add(pair.Key);
            foreach(ushort id in endedSeats)EndSeat(id,true,true);
        }
        internal void SeatGui(ComponentGui gui)
        {
            var p=gui?.m_componentPlayer;if(p==null||!p.PlayerData.IsMainPlayer)return;
            HomeSeatHud hud;
            if(!HasSeat(p)){if(seatHuds.TryGetValue(p,out hud)){hud.Dispose();seatHuds.Remove(p);}return;}
            if(!seatHuds.TryGetValue(p,out hud)){if(seatHuds.Count>=4)return;hud=new HomeSeatHud(gui);seatHuds.Add(p,hud);}
            hud.Update(this);
        }
        private void ClearSeats()
        {
            foreach(var s in seats.Values)RestoreSeat(s);seats.Clear();foreach(var h in seatHuds.Values)h.Dispose();seatHuds.Clear();
            lock(seatRequests)seatRequests.Clear();seatVersions.Clear();seatPeers.Clear();endedSeats.Clear();
        }
    }
    internal sealed class HomeSeatHud : IDisposable
    {
        private readonly ComponentGui gui;private readonly CanvasWidget root=new CanvasWidget{IsHitTestVisible=false};
        private readonly CanvasWidget slot=new CanvasWidget{Size=new Vector2(88,72),IsHitTestVisible=false};
        private readonly BevelledButtonWidget stand=new BevelledButtonWidget{Text="起身",Size=new Vector2(88,64),FontScale=.65f,CenterColor=new Color(48,57,67),BevelColor=new Color(164,177,189)};
        internal HomeSeatHud(ComponentGui gui){this.gui=gui;root.Children.Add(slot);slot.Children.Add(stand);gui.ControlsContainerWidget.Children.Add(root);}
        internal void Update(SubsystemHomeFurniture runtime)
        {
            var p=gui.m_componentPlayer;root.IsVisible=p.IsAddedToProject&&p.ComponentHealth.Health>0&&gui.ModalPanelWidget==null&&!DialogsManager.HasDialogs(p.GuiWidget)&&p.GameWidget.ActiveCamera.IsEntityControlEnabled;
            if(root.IsVisible&&Artifacts.ArtifactSkillHud.Arrange(gui,root,slot)&&stand.IsClicked)runtime.RequestStand(p);
        }
        public void Dispose(){if(root.ParentWidget!=null)root.ParentWidget.Children.Remove(root);}
    }
}
