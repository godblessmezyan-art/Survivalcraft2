"""Author mod-owned overrides; native Rhino template metadata is inherited, no game art copied."""
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
def guid(key):return str(uuid.uuid5(uuid.NAMESPACE_URL,'https://scieport.local/bone-dragon/'+key))
doc=ET.Element('SCIEPortBoneDragon_Saddled')
folder=ET.SubElement(doc,'Folder',Name='Entities',Guid='5fcdf35f-7001-442c-883e-e0502b15d291')
entity=ET.SubElement(folder,'EntityTemplate',Name='SCIEPortBoneDragon_Saddled',Guid=guid('Entity'),InheritanceParent='4d02fd7e-3054-4121-b2a5-787db985df5f')
groups={
 'Creature':{'DisplayName':('骨龙','string'),'Description':('死亡权柄唯一骨龙坐骑。生命444；近战37伤害/1秒，地速7、飞速16。','string'),'ConstantSpawn':('True','bool'),'KillVerbs':('mauled','string')},
 'FourLeggedModel':{'Class':('SCIEPort.Features.AbyssReaper.ComponentBoneDragonModel','string'),'ModelName':('Models/AbyssBoneDragon','string'),'TextureOverride':('Textures/AbyssBoneDragon','string'),'BoundingSphereRadius':('6','float'),'WalkAnimationSpeed':('0.65','float'),'WalkFrontLegsAngle':('0.44','float'),'WalkHindLegsAngle':('0.48','float'),'WalkBobHeight':('0.035','float'),'CanCanter':('False','bool'),'CanTrot':('False','bool'),'UseCanterSound':('False','bool')},
 'Body':{'BoxSize':('2.1,2.7,5.8','Vector3'),'Mass':('1800','float')},
 'Health':{'AttackResilience':('444','float'),'FireResilience':('40','float'),'FallResilience':('12','float')},
 'Locomotion':{'Class':('SCIEPort.Features.AbyssReaper.ComponentBoneDragonLocomotion','string'),'FlySpeed':('16','float'),'WalkSpeed':('7','float'),'JumpSpeed':('7','float'),'TurnSpeed':('4','float'),'AccelerationFactor':('0.7','float')},
 'Mount':{'Class':('Game.ComponentMount','string'),'MountOffset':('0,2.1,0.16','Vector3'),'DismountOffset':('1.8,0.3,0.16','Vector3'),'LoadOrder':('0','int'),'IsOptional':('False','bool')},
 'SteedBehavior':{'Class':('Game.ComponentSteedBehavior','string'),'LoadOrder':('0','int'),'IsOptional':('False','bool')},
 'Miner':{'AttackPower':('37','float')},
 'Spawn':{'AutoDespawn':('False','bool')},
 'HerdBehavior':{'HerdName':('SCIEPortBoneDragon_Saddled','string'),'AutoNearbyCreaturesHelp':('False','bool')},
 'ChaseBehavior':{'AutoChaseMask':('0','Game.CreatureCategory'),'ChaseWhenAttackedProbability':('0','float'),'ChaseOnTouchProbability':('0','float'),'ChaseNonPlayerProbability':('0','float')},
 'Loot':{'LootDropped':('True','bool')},
 'CreatureEggData':{'EggTypeIndex':('-1','int'),'ShowEgg':('False','bool')}
}
for group,parameters in groups.items():
 node=ET.SubElement(entity,'ParameterSet' if group=='CreatureEggData' else 'MemberComponentTemplate',Name=group,Guid=guid(group))
 for name,(value,typ) in parameters.items():ET.SubElement(node,'Parameter',Name=name,Guid=guid(group+'/'+name),Value=value,Type=typ)
ET.indent(doc);ET.ElementTree(doc).write(root/'src/SCIEPort/Assets/Database/AbyssBoneDragon.xdb',encoding='utf-8',xml_declaration=True)
path=root/'src/SCIEPort/Assets/Database/AbyssBoneDragon.xdb';path.write_bytes(path.read_bytes().replace(b'\r\n',b'\n'))
print('Authored original bone dragon overrides; native steed, custom flying locomotion. No item/egg ID.')
