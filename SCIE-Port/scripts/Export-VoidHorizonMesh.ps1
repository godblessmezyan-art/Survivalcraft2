param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputDir,
    [float[]]$Times=@(.3,.5,.8,1.3,2.5,3.3,3.8,4.1,4.2)
)
$ErrorActionPreference='Stop'
foreach($name in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $name)) }
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$source=@'
using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using Engine;
using Engine.Graphics;
public static class VoidMeshExport
{
    public static void Run(Assembly mod, string output, float[] times)
    {
        Directory.CreateDirectory(output);
        Type type=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonScene",true);
        BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object scene=Activator.CreateInstance(type,true);
        try
        {
            var queue=type.GetMethod("Queue",flags);
            foreach(float time in times)
            {
                int claimed=(int)queue.Invoke(scene,new object[]{Vector3.Zero,Vector3.UnitZ,time,1});
                var renderer=(PrimitivesRenderer3D)type.GetField("renderer",flags).GetValue(scene);
                renderer.m_allBatches.Sort((a,b)=>a.Layer.CompareTo(b.Layer));
                string suffix=time.ToString("0.0",CultureInfo.InvariantCulture);
                int count=0;
                using(var writer=new StreamWriter(Path.Combine(output,"mesh-"+suffix+".tsv")))
                {
                    writer.WriteLine("# Actual compiled VoidHorizonScene triangles; CPU queue only, not gameplay capture.");
                    foreach(BaseBatch item in renderer.m_allBatches)
                    {
                        var batch=item as FlatBatch3D;if(batch==null)continue;
                        for(int i=0;i<batch.TriangleIndices.Count;i+=3)
                        {
                            writer.Write(item.Layer+"\t"+(item.BlendState==BlendState.Opaque?"1":"0"));
                            for(int j=0;j<3;j++)
                            {
                                var v=batch.TriangleVertices.Array[batch.TriangleIndices.Array[i+j]];
                                float[] fields={v.Position.X,v.Position.Y,v.Position.Z,v.Color.R,v.Color.G,v.Color.B,v.Color.A};
                                foreach(float value in fields)
                                {
                                    if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Non-finite mesh at t="+time+" triangle="+count);
                                    writer.Write("\t"+value.ToString("R",CultureInfo.InvariantCulture));
                                }
                            }
                            count++;writer.WriteLine();
                        }
                    }
                }
                if(count!=claimed)throw new Exception("Triangle count disagrees with queue at t="+time);
                Console.WriteLine("Void domain mesh "+suffix+": "+count+" triangles, all finite.");
            }
        }
        finally { ((IDisposable)scene).Dispose(); }
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies @((Join-Path $GameDir 'Engine.dll'))
[VoidMeshExport]::Run($mod,$OutputDir,$Times)
