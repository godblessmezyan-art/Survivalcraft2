using System;
using System.Reflection;
using System.Runtime.Serialization;
using Game.NetWork;
using Game.NetWork.Packages;
using LiteNetLib;

// Execute the target's actual SyncList handler. No socket, game or simulated ID equality.
public static class NativeServerSession
{
    static void Put(object o,string name,object value)
    {o.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(o,value,null);}
    public static Client Install(NetNode node)
    {
        var peer=(NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var bootstrap=new Client(Guid.NewGuid(),null);Put(bootstrap,"Peer",peer);
        var server=new Client(peer,0,Guid.NewGuid(),Guid.NewGuid(),null,"-1","host");
        var self=new Client(null,2,Guid.NewGuid(),Guid.NewGuid(),null,"-1","guest");node.TokenId=self.TokenId;
        var source=new ClientPackage(new[]{server,self});var writer=new PackageStreamWriter();source.WriteData(writer);
        var incoming=new ClientPackage();using(var reader=new PackageStreamReader(writer.Data()))incoming.ReadData(reader);writer.Dispose();
        incoming.From=bootstrap;incoming.Handle(null,node,false);
        return node.Clients[0];
    }
}
