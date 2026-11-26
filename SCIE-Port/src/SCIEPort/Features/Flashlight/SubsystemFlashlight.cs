#pragma warning disable CS1591
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Flashlight
{
    public sealed class SubsystemFlashlight : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        private const int BeforeTerrain = -1;
        private static readonly int[] s_drawOrders = { BeforeTerrain, 101 };
        private static readonly int[] s_blocks = { FlashlightBlock.Index };
        private readonly FlashlightTerrainLighting m_light = new FlashlightTerrainLighting();
        private readonly Dictionary<ComponentPlayer, double> m_partialSeconds = new Dictionary<ComponentPlayer, double>();
        private readonly Dictionary<ComponentPlayer, FlashlightPackage> m_pending = new Dictionary<ComponentPlayer, FlashlightPackage>();
        private SubsystemPlayers m_players;

        public override int[] HandledBlocks => s_blocks;
        public int[] DrawOrders => s_drawOrders;
        public UpdateOrder UpdateOrder => UpdateOrder.Locomotion;

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
        }

        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            ComponentPlayer player = miner.ComponentPlayer;
            IInventory inventory = miner.Inventory;
            int value = miner.ActiveBlockValue;
            if (Terrain.ExtractContents(value) != FlashlightBlock.Index) return false;
            // Remote native interaction is also replayed. Only the authenticated request handles its switch.
            if (!CanUse(player) || !player.PlayerData.IsMainPlayer || inventory == null) return true;
            if (FlashlightBlock.Spent(Terrain.ExtractData(value)) == FlashlightBlock.Capacity)
            {
                player.ComponentGui.DisplaySmallMessage("电量耗尽", Color.White, false, false);
                return true;
            }
            int slot = inventory.ActiveSlotIndex;
            bool on = !FlashlightBlock.IsOn(value);
            if (CommonLib.WorkType == WorkType.Client)
            {
                if (slot >= 0 && slot < 10 && CommonLib.Net?.Server != null)
                    Enqueue(player, new FlashlightPackage { To = CommonLib.Net.Server, Slot = (byte)slot, TurnOn = on });
            }
            else
                ApplySwitch(player, slot, on);
            return true;
        }

        internal void Enqueue(ComponentPlayer player, FlashlightPackage request)
        {
            lock (m_pending)
                m_pending[player] = request;
        }

        public void Update(float dt)
        {
            bool client = CommonLib.WorkType == WorkType.Client;
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                IInventory inventory = player.ComponentMiner.Inventory;
                int slot = inventory?.ActiveSlotIndex ?? -1;
                FlashlightPackage request;
                lock (m_pending)
                {
                    m_pending.TryGetValue(player, out request);
                    // ComponentPlayer announces shortcut changes at the start of its next update.
                    if (client && request != null && request.Slot == slot && player.LastActiveSlot != slot)
                        continue;
                    if (request != null) m_pending.Remove(player);
                }
                if (!CanUse(player) || inventory == null) continue;
                if (client)
                {
                    if (request != null && request.Slot == slot && ReferenceEquals(request.To, CommonLib.Net?.Server) &&
                        FlashlightBlock.Index == Terrain.ExtractContents(inventory.GetSlotValue(slot)))
                        CommonLib.Net.QueuePackage(request);
                    continue;
                }
                if (request != null && request.From != null && ReferenceEquals(request.From, player.PlayerData.Client))
                    ApplySwitch(player, request.Slot, request.TurnOn);

                int value = inventory.GetSlotValue(slot);
                if (!FlashlightBlock.IsOn(value) || !(dt > 0f)) continue;
                m_partialSeconds.TryGetValue(player, out double elapsed);
                elapsed += dt;
                int seconds = (int)elapsed;
                m_partialSeconds[player] = elapsed - seconds;
                if (seconds > 0) ReplaceSlot(inventory, slot, FlashlightBlock.Drain(value, seconds));
            }
        }

        private static bool CanUse(ComponentPlayer player) => player != null && player.ComponentHealth.Health > 0f;

        private static void ApplySwitch(ComponentPlayer player, int slot, bool on)
        {
            IInventory inventory = player.ComponentMiner.Inventory;
            if (inventory == null || slot != inventory.ActiveSlotIndex || slot < 0 || slot >= 10) return;
            int value = inventory.GetSlotValue(slot);
            if (Terrain.ExtractContents(value) != FlashlightBlock.Index) return;
            int next = FlashlightBlock.SetPower(value, on);
            if (value != next) ReplaceSlot(inventory, slot, next);
        }

        private static void ReplaceSlot(IInventory inventory, int slot, int value)
        {
            int count = inventory.GetSlotCount(slot);
            if (count <= 0) return;
            inventory.SetSlotValue(slot, new ComponentInventoryBase.Slot { Count = count, Value = value });
            // Survival SetSlotValue queues a native sync; the creative implementation only sets the value.
            if (CommonLib.WorkType == WorkType.Server && inventory is ComponentCreativeInventory creative)
                creative.OnSlotChange(slot);
        }

        public void Draw(Camera camera, int drawOrder)
        {
            m_light.End();
            if (drawOrder != BeforeTerrain || !camera.IsEntityControlEnabled) return;
            ComponentPlayer player = camera.GameWidget.PlayerData.ComponentPlayer;
            if (CanUse(player) && FlashlightBlock.IsOn(player.ComponentMiner.ActiveBlockValue))
                m_light.Begin(camera);
        }

        public override void OnEntityRemoved(Entity entity)
        {
            ComponentPlayer player = entity.FindComponent<ComponentPlayer>(false);
            if (player != null)
            {
                m_partialSeconds.Remove(player);
                lock (m_pending)
                    m_pending.Remove(player);
            }
            base.OnEntityRemoved(entity);
        }

        public override void Dispose()
        {
            m_light.Dispose();
            m_partialSeconds.Clear();
            lock (m_pending)
                m_pending.Clear();
            base.Dispose();
        }
    }
}
