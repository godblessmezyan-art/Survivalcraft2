"""Author mod-owned overrides; native Rhino template metadata is inherited, no game art copied."""
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
def guid(key):return str(uuid.uuid5(uuid.NAMESPACE_URL,'https://scieport.local/cerberus/'+key))
doc=ET.Element('SCIEPortCerberus')
folder=ET.SubElement(doc,'Folder',Name='Entities',Guid='5fcdf35f-7001-442c-883e-e0502b15d291')
entity=ET.SubElement(folder,'EntityTemplate',Name='SCIEPortCerberus',Guid=guid('Entity'),InheritanceParent='4d02fd7e-3054-4121-b2a5-787db985df5f')
groups={
 'Creature':{'DisplayName':('地狱三头犬·刻尔柏洛斯','string'),'Description':('死亡权柄唯一契约坐骑。三首噬魂37伤害/1秒；四格跳跃，可切换骑乘自动追击。','string'),'ConstantSpawn':('True','bool'),'KillVerbs':('mauled','string')},
 'FourLeggedModel':{'Class':('SCIEPort.Features.AbyssReaper.ComponentCerberusModel','string'),'ModelName':('Models/AbyssCerberus','string'),'TextureOverride':('Textures/AbyssCerberus','string'),'BoundingSphereRadius':('3.5','float'),'WalkAnimationSpeed':('0.65','float'),'WalkFrontLegsAngle':('0.44','float'),'WalkHindLegsAngle':('0.48','float'),'WalkBobHeight':('0.035','float'),'CanCanter':('False','bool'),'CanTrot':('False','bool'),'UseCanterSound':('False','bool')},
 'Body':{'BoxSize':('1.8,2.5,3.6','Vector3'),'Mass':('1800','float')},
 'Health':{'AttackResilience':('444','float'),'FireResilience':('40','float'),'FallResilience':('12','float')},
 'Locomotion':{'WalkSpeed':('7','float'),'JumpSpeed':('10','float'),'TurnSpeed':('4','float'),'AccelerationFactor':('0.7','float')},
 'Mount':{'Class':('Game.ComponentMount','string'),'MountOffset':('0,2.08,0.5','Vector3'),'DismountOffset':('1.8,0.3,0','Vector3'),'LoadOrder':('0','int'),'IsOptional':('False','bool')},
 'SteedBehavior':{'Class':('SCIEPort.Features.AbyssReaper.ComponentCerberusSteed','string'),'LoadOrder':('0','int'),'IsOptional':('False','bool')},
 'Miner':{'AttackPower':('37','float')},
 'Spawn':{'AutoDespawn':('False','bool')},
 'HerdBehavior':{'HerdName':('SCIEPortCerberus','string'),'AutoNearbyCreaturesHelp':('False','bool')},
 'ChaseBehavior':{'AutoChaseMask':('0','Game.CreatureCategory'),'ChaseWhenAttackedProbability':('0','float'),'ChaseOnTouchProbability':('0','float'),'ChaseNonPlayerProbability':('0','float')},
 'CreatureSounds':{'AttackSound':('Audio/Creatures/WolfBark','string'),'IdleSound':('Audio/Creatures/WolfGrowl','string'),'PainSound':('Audio/Creatures/WolfBark','string')},
 'Loot':{'LootDropped':('True','bool')},
 'CreatureEggData':{'EggTypeIndex':('-1','int'),'ShowEgg':('False','bool')}
}
for group,parameters in groups.items():
 node=ET.SubElement(entity,'ParameterSet' if group=='CreatureEggData' else 'MemberComponentTemplate',Name=group,Guid=guid(group))
 for name,(value,typ) in parameters.items():ET.SubElement(node,'Parameter',Name=name,Guid=guid(group+'/'+name),Value=value,Type=typ)
ET.indent(doc);ET.ElementTree(doc).write(root/'src/SCIEPort/Assets/Database/AbyssCerberus.xdb',encoding='utf-8',xml_declaration=True)
path=root/'src/SCIEPort/Assets/Database/AbyssCerberus.xdb';path.write_bytes(path.read_bytes().replace(b'\r\n',b'\n'))
print('Authored Cerberus entity overrides with native mount and steed components; no item/egg ID.')
