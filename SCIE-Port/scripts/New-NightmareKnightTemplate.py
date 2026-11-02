"""Original cavalry metadata. Inherit installed Rhino defaults, never package game art."""
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
def guid(key):return str(uuid.uuid5(uuid.NAMESPACE_URL,'https://scieport.local/nightmare-knight/'+key))
doc=ET.Element('SCIEPortNightmareKnight')
folder=ET.SubElement(doc,'Folder',Name='Entities',Guid='5fcdf35f-7001-442c-883e-e0502b15d291')
entity=ET.SubElement(folder,'EntityTemplate',Name='SCIEPortNightmareKnight',Guid=guid('Entity'),InheritanceParent='4d02fd7e-3054-4121-b2a5-787db985df5f')
groups={
 'Creature':{'DisplayName':('夜魇骑士','string'),'Description':('冥界军势骑兵。','string'),'ConstantSpawn':('True','bool'),'KillVerbs':('slashed','string')},
 'FourLeggedModel':{'Class':('SCIEPort.Features.AbyssReaper.ComponentNightmareKnightModel','string'),'ModelName':('Models/AbyssNightmareKnight','string'),'TextureOverride':('Textures/AbyssNightmareKnight','string'),'BoundingSphereRadius':('4','float')},
 'Body':{'BoxSize':('1.8,3.65,3.8','Vector3'),'Mass':('600','float')},
 'Health':{'AttackResilience':('70','float'),'FireResilience':('40','float'),'FallResilience':('6','float')},
 'Locomotion':{'WalkSpeed':('8','float'),'FlySpeed':('0','float'),'JumpSpeed':('7','float'),'TurnSpeed':('4','float'),'AccelerationFactor':('0.7','float')},
 'Miner':{'AttackPower':('15','float')},
 'Spawn':{'AutoDespawn':('False','bool')},
 'HerdBehavior':{'HerdName':('SCIEPortNightmareKnight','string'),'AutoNearbyCreaturesHelp':('False','bool')},
 'ChaseBehavior':{'AutoChaseMask':('0','Game.CreatureCategory'),'ChaseWhenAttackedProbability':('0','float'),'ChaseOnTouchProbability':('0','float'),'ChaseNonPlayerProbability':('0','float')},
 'CreatureSounds':{'IdleSound':('Audio/Creatures/Horse','string'),'PainSound':('Audio/Creatures/Horse','string'),'AttackSound':('Audio/Creatures/Horse','string')},
 'Loot':{'LootDropped':('True','bool')},
 'CreatureEggData':{'EggTypeIndex':('-1','int'),'ShowEgg':('False','bool')}
}
for group,parameters in groups.items():
 node=ET.SubElement(entity,'ParameterSet' if group=='CreatureEggData' else 'MemberComponentTemplate',Name=group,Guid=guid(group))
 for name,(value,typ) in parameters.items():ET.SubElement(node,'Parameter',Name=name,Guid=guid(group+'/'+name),Value=value,Type=typ)
ET.indent(doc);ET.ElementTree(doc).write(ROOT/'src/SCIEPort/Assets/Database/AbyssNightmareKnight.xdb',encoding='utf-8',xml_declaration=True)
print('Original knight template:',guid('Entity'),'single creature; no player mount or item/egg ID.')
