// Original low-poly armor. Node built-ins only. Native male bind-space converted to Y_UP meters.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateSync } from 'node:zlib';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const assetDir=path.join(root,'src/SCIEPort/Assets');
const previewDir=path.join(root,'docs/assets/four-saints-v3');
fs.mkdirSync(previewDir,{recursive:true});
// Lining and ancient bronze match the Qinglong crown. Remaining swatches are opaque materials.
const palette=[[24,38,43],[28,31,38],[51,59,67],[81,94,101],
 [106,76,43],[178,137,73],[228,235,237],[157,179,188],
 [69,83,94],[157,37,35],[205,60,43],[244,177,77],
 [82,42,34],[225,195,119],[54,135,140],[49,161,132]];
const add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]);
const mul=(a,k)=>a.map(v=>v*k),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const unit=a=>mul(a,1/Math.hypot(...a)),fmt=n=>Number(n.toFixed(6)).toString();
let parts=[],bone='Body';
function part(name,m){const p={name,bone,m,tri:[]};parts.push(p);return p;}
function tri(p,a,b,c,inside){let n=cross(sub(b,a),sub(c,a));
 if(Math.hypot(...n)<1e-10)throw Error('Degenerate '+p.name);
 if(dot(n,sub(mul(add(add(a,b),c),1/3),inside))<0){[b,c]=[c,b];n=mul(n,-1);}
 p.tri.push({v:[a,b,c],n:unit(n),m:p.m});}
function quad(p,a,b,c,d,inside){tri(p,a,b,c,inside);tri(p,a,c,d,inside);}
// Convex faceted plate in any plane; ridge is real solid geometry, not a decal.
function prism(name,outline,offset,m,ridge=0){const p=part(name,m),base=mul(outline.reduce(add,[0,0,0]),1/outline.length),inside=add(base,mul(offset,.5));
 const front=add(base,mul(unit(offset),-ridge)),back=add(base,offset);
 for(let i=0;i<outline.length;i++){const j=(i+1)%outline.length,a=outline[i],b=outline[j],c=add(b,offset),d=add(a,offset);
 tri(p,front,a,b,inside);tri(p,back,c,d,inside);quad(p,a,d,c,b,inside);}return p;}
function plate(name,xy,z,depth,m,ridge=0){return prism(name,xy.map(([x,y])=>[x,y,z]),[0,0,depth],m,ridge);}
function hex(name,x,y,z,rx,ry,m,ridge=.009,depth=.012){return plate(name,
 [[x,y+ry],[x+rx,y+ry*.48],[x+rx,y-ry*.48],[x,y-ry],[x-rx,y-ry*.48],[x-rx,y+ry*.48]],z,depth,m,ridge);}
function tube(name,points,radii,m,sides=6){const p=part(name,m),rings=[];
 for(let k=0;k<points.length;k++){const axis=unit(sub(points[Math.min(k+1,points.length-1)],points[Math.max(0,k-1)]));
 const e=unit(cross(Math.abs(axis[1])<.9?[0,1,0]:[1,0,0],axis)),f=cross(axis,e);
 rings.push(Array.from({length:sides},(_,i)=>add(points[k],add(mul(e,Math.cos(i*2*Math.PI/sides)*radii[k]),mul(f,Math.sin(i*2*Math.PI/sides)*radii[k])))));}
 for(let k=0;k<points.length-1;k++)for(let i=0;i<sides;i++){const j=(i+1)%sides;quad(p,rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i],mul(add(points[k],points[k+1]),.5));}
 for(let i=0;i<sides;i++){tri(p,points[0],rings[0][(i+1)%sides],rings[0][i],points[1]);let k=points.length-1;tri(p,points[k],rings[k][i],rings[k][(i+1)%sides],points[k-1]);}return p;}
function dome(name,c,rx,ry,rz,m){const p=part(name,m),rings=[];
 // Three upper bands with a tiny, nonzero top ring; underside remains hollow.
 for(const a of [.08,.60,1.10,1.50])rings.push(Array.from({length:8},(_,i)=>[c[0]+rx*Math.sin(a)*Math.cos(i*Math.PI/4),c[1]+ry*Math.cos(a),c[2]+rz*Math.sin(a)*Math.sin(i*Math.PI/4)]));
 for(let k=0;k<rings.length-1;k++)for(let i=0;i<8;i++){const j=(i+1)%8;quad(p,rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i],c);}
 for(let i=0;i<8;i++){tri(p,[c[0],c[1]+ry,c[2]],rings[0][i],rings[0][(i+1)%8],c);}
 // Closed rim, inset shell, rather than a shoulder-sized solid block.
 const outer=rings[3],inner=outer.map(v=>add(c,mul(sub(v,c),.90)));
 for(let i=0;i<8;i++){const j=(i+1)%8;quad(p,outer[i],inner[i],inner[j],outer[j],mul(add(outer[i],inner[j]),.5));}
 return p;}
function source(id,data,stride,params){return `<source id="${id}"><float_array id="${id}-array" count="${data.length}">${data.map(fmt).join(' ')}</float_array><technique_common><accessor source="#${id}-array" count="${data.length/stride}" stride="${stride}">${params.map(n=>`<param name="${n}" type="float"/>`).join('')}</accessor></technique_common></source>`;}
function geometry(p){const pos=p.tri.flatMap(t=>t.v.flat()),normal=p.tri.flatMap(t=>[...t.n,...t.n,...t.n]),u=(p.m*16+8)/256,uv=p.tri.flatMap(()=>[u,.5,u,.5,u,.5]),id=p.name;
 return `<geometry id="${id}-mesh" name="${id}"><mesh>${source(id+'-positions',pos,3,['X','Y','Z'])}${source(id+'-normals',normal,3,['X','Y','Z'])}${source(id+'-uv',uv,2,['S','T'])}<vertices id="${id}-vertices"><input semantic="POSITION" source="#${id}-positions"/></vertices><triangles count="${p.tri.length}"><input semantic="VERTEX" source="#${id}-vertices" offset="0"/><input semantic="NORMAL" source="#${id}-normals" offset="1"/><input semantic="TEXCOORD" source="#${id}-uv" offset="2" set="0"/><p>${Array.from({length:pos.length/3},(_,i)=>`${i} ${i} ${i}`).join(' ')}</p></triangles></mesh></geometry>`;}
const identity='1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1';
function save(name){const bones=[...new Set(parts.map(p=>p.bone))],node=b=>`<node id="${b}" name="${b}" type="NODE"><matrix>${identity}</matrix>${parts.filter(p=>p.bone===b).map(p=>`<instance_geometry url="#${p.name}-mesh"/>`).join('')}</node>`;
 const nodes=bones.length===1?node(bones[0]):`<node id="ArmorRoot" name="ArmorRoot" type="NODE"><matrix>${identity}</matrix>${bones.map(node).join('')}</node>`;
 const dae=`<?xml version="1.0" encoding="utf-8"?>\n<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1"><asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-FourSaintsArmorV3Assets.mjs</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset><library_geometries>${parts.map(geometry).join('\n')}</library_geometries><library_visual_scenes><visual_scene id="Scene" name="Scene">${nodes}</visual_scene></library_visual_scenes><scene><instance_visual_scene url="#Scene"/></scene></COLLADA>\n`;
 fs.writeFileSync(path.join(assetDir,'Models',name+'.dae'),dae);
 const points=parts.flatMap(p=>p.tri.flatMap(t=>t.v));
 const report={name,bones:bones.length===1?1:3,meshes:parts.length,triangles:parts.reduce((s,p)=>s+p.tri.length,0),bounds:[0,1,2].map(i=>[Math.min(...points.map(v=>v[i])),Math.max(...points.map(v=>v[i]))]),parts:parts.map(p=>({name:p.name,bone:p.bone,triangles:p.tri.length}))};
 fs.writeFileSync(path.join(previewDir,name+'.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({name,bones:report.bones,meshes:report.meshes,triangles:report.triangles,bounds:report.bounds}));
 const result={name,parts,report};parts=[];return result;
}

// XUANWU: body bind origin is near the feet, not the centre of the chest.
bone='Body';
plate('CuirassLining',[[-.17,.80],[-.194,1.16],[-.15,1.33],[.15,1.33],[.194,1.16],[.17,.80]],-.153,.025,0,.008);
plate('BronzeBreastFrame',[[-.15,.83],[-.185,1.16],[-.13,1.315],[.13,1.315],[.185,1.16],[.15,.83]],-.170,.018,4,.008);
for(let row=0;row<3;row++)for(let col=0;col<3;col++){
 const x=(col-1)*.103,y=1.225-row*.125+(col===1?.022:0);
 hex('ChestScale'+row+col,x,y,-.191-row*.002,.053,.070,row%2===0?2:1,.015);
 if(row===0)hex('ScaleInset'+col,x,y,-.208,.036,.047,3,.005,.005);
}
plate('BackShell',[[-.146,.85],[-.183,1.10],[-.13,1.30],[0,1.345],[.13,1.30],[.183,1.10],[.146,.85],[0,.80]],.123,-.026,1,.047);
for(const s of [-1,1]){
 const side=s<0?'Left':'Right';
 dome('TurtleShoulder'+side,[s*.225,1.23,-.006],.111,.112,.152,2);
 hex('ShoulderHex'+side,s*.232,1.253,-.145,.073,.060,4,.012);
 hex('ShoulderShell'+side,s*.232,1.253,-.160,.057,.044,1,.014);
 tube('ShoulderBronzeLink'+side,[[s*.135,1.293,-.10],[s*.19,1.31,-.105]],[.015,.015],5,6);
 // Waist plates end above the native hip pivot (0.712m); leg swing is not locked to the torso.
 for(let i=0;i<2;i++){const x=s*(.057+i*.080);plate('Skirt'+side+i,[[x-.038,.892],[x+.038,.892],[x+.033,.769],[x,.744],[x-.033,.769]],-.146,.018,2,.012);}
 plate('BackSkirt'+side,[[s*.012,.88],[s*.14,.88],[s*.135,.765],[s*.02,.756]],.132,-.015,2,.008);
 prism('SideRib'+side,[[s*.188,.92,-.09],[s*.205,1.18,-.10],[s*.205,1.18,.07],[s*.188,.92,.08]],[s*.012,0,0],1,.005);
}
hex('WaistClasp',0,.865,-.177,.038,.035,5,.009);
// Raised coiled snake in front of the shell layers, with separate head and jade eyes.
const coil=Array.from({length:17},(_,i)=>{const a=i*Math.PI*2/16;return [.061*Math.cos(a),1.104+.049*Math.sin(a),-.231];});
tube('CoiledSnake',coil,coil.map(()=>.011),5,6);
tube('SnakeNeck',[[.055,1.09,-.232],[.066,1.137,-.238],[.039,1.166,-.242],[.011,1.162,-.247]],[.012,.013,.015,.017],4,6);
plate('SnakeHead',[[-.009,1.163],[.011,1.184],[.034,1.166],[.011,1.147]],-.267,.023,5,.006);
for(const s of [-1,1])hex('SnakeEye'+s,.012+s*.008,1.168,-.277,.003,.004,15,.002,.003);
for(const y of [.94,1.08,1.22])hex('BackHex'+y,0,y,.172,.066,.071,2,.010,-.008);
const xuanwu=save('XuanwuArmorV3');

// BAIHU: paired, asymmetric parts authored around each native hip pivot.
for(const s of [-1,1]){bone=s<0?'Leg1':'Leg2';const side=s<0?'Left':'Right',cx=s*.065;
 for(let i=0;i<3;i++){const y=-.075-i*.071;
 plate('ThighPlate'+side+i,[[cx-.059,y+.036],[cx+.059,y+.036],[cx+.056,y-.024],[cx,y-.048],[cx-.056,y-.024]],-.099-i*.004,.018,i%2===0?7:6,.009);
 // Graphite tiger chevrons are inset solid applique strips on each metal segment.
 plate('TigerStripe'+side+i,[[cx-.047,y+.023],[cx-.012,y+.013],[cx+.039,y+.022],[cx+.016,y+.008],[cx-.012,y-.001],[cx-.043,y+.008]],-.112-i*.004,.004,8,.002);
 }
 hex('TigerKneeFrame'+side,cx,-.357,-.118,.066,.075,5,.010);
 plate('TigerKneeMask'+side,[[cx-.059,-.314],[cx-.042,-.291],[cx,-.315],[cx+.042,-.291],[cx+.059,-.314],[cx+.052,-.385],[cx,-.416],[cx-.052,-.385]],-.137,.025,6,.022);
 for(const e of [-1,1]){
 const x=cx+e*.027;
 plate('TigerEye'+side+e,[[x-.014,-.339],[x+.014,-.344],[x+.010,-.352],[x-.011,-.349]],-.165,.006,8,.003);
 tube('TigerFang'+side+e,[[cx+e*.027,-.391,-.161],[cx+e*.030,-.423,-.171],[cx+e*.027,-.455,-.162]],[.011,.008,.0015],6,5);
 }
 hex('TigerMuzzle'+side,cx,-.379,-.168,.026,.024,7,.011);
 plate('TigerNose'+side,[[cx-.013,-.365],[cx+.013,-.365],[cx,-.379]],-.185,.008,8,.006);
 plate('ShinPlate'+side,[[cx-.052,-.448],[cx+.052,-.448],[cx+.042,-.590],[cx,-.618],[cx-.042,-.590]],-.102,.018,7,.013);
 // Outward, rearward sweeping guard; never mirror across the inter-leg gap at draw time.
 prism('SweptShin'+side,[[s*.135,-.454,-.020],[s*.135,-.493,.056],[s*.135,-.540,.157],[s*.135,-.566,.086],[s*.135,-.589,-.027]],[s*.016,0,0],6,.006);
 tube('ShinGoldSpine'+side,[[cx,-.468,-.120],[cx,-.548,-.125],[cx,-.598,-.115]],[.006,.007,.004],5,5);
 // Rear metal segment keeps the side/back silhouette distinct from a painted leg.
 plate('CalfRear'+side,[[cx-.044,-.454],[cx+.044,-.454],[cx+.034,-.568],[cx-.034,-.568]],.090,-.012,8,.008);
}
const baihu=save('BaihuLegArmorV3');

// ZHUQUE: soles at -0.716m relative to the hip; feather blades stay outside each ankle.
for(const s of [-1,1]){bone=s<0?'Leg1':'Leg2';const side=s<0?'Left':'Right',cx=s*.065;
 const footprint=[[cx-.067,-.196],[cx,-.248],[cx+.067,-.196],[cx+.074,.070],[cx+.050,.101],[cx-.050,.101],[cx-.074,.070]];
 prism('DarkSole'+side,footprint.map(([x,z])=>[x,-.716,z]),[0,.021,0],0);
 prism('SoleGoldRim'+side,footprint.map(([x,z])=>[x,-.694,z]),[0,.007,0],5);
 // Hollow upper shell, with a bird-beak point instead of a mechanical squared toe.
 prism('PhoenixToe'+side,[[cx,-.675,-.243],[cx+.060,-.672,-.186],[cx+.060,-.651,-.061],[cx,-.619,-.083],[cx-.060,-.651,-.061],[cx-.060,-.672,-.186]],[0,-.012,0],9,.009);
 tube('BeakGoldRidge'+side,[[cx,-.679,-.242],[cx,-.656,-.190],[cx,-.628,-.097]],[.003,.007,.009],11,5);
 plate('AnkleFront'+side,[[cx-.053,-.548],[cx+.053,-.548],[cx+.056,-.645],[cx,-.669],[cx-.056,-.645]],-.087,.018,10,.010);
 plate('AnkleInset'+side,[[cx-.020,-.558],[cx+.020,-.558],[cx,-.617]],-.106,.008,11,.007);
 plate('HeelLeather'+side,[[cx-.052,-.563],[cx+.052,-.563],[cx+.056,-.679],[cx-.056,-.679]],.077,-.013,12,.005);
 for(let i=0;i<3;i++){
 const x=s*(.139+i*.005),y=-.637+i*.020,z=-.022+i*.025;
 const outline=[[x,y,z],[x,y+.046,z+.020],[x,y+.146-i*.012,z+.118],[x,y+.103-i*.008,z+.130],[x,y+.012,z+.045]];
 prism('AnkleFeather'+side+i,outline,[s*.006,0,0],i===1?10:9,.003);
 const tip=outline[2];tube('FeatherQuill'+side+i,[add(outline[0],[s*.007,0,0]),add(tip,[s*.007,-.010,-.008])],[.003,.0015],11,4);
 }
 for(let i=0;i<2;i++){
 const x=cx+(i===0?-.022:.022);
 prism('HeelTail'+side+i,[[x,-.657,.084],[x,-.627,.096],[x,-.535,.189],[x,-.592,.172]],[.008,0,0],i===0?10:11,.002);
 }
}
const zhuque=save('ZhuqueBootsV3');

function crc32(data){let c=0xffffffff;for(const b of data){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function png(w,h,rgba){const chunk=(type,d)=>{const t=Buffer.from(type),l=Buffer.alloc(4),c=Buffer.alloc(4);l.writeUInt32BE(d.length);c.writeUInt32BE(crc32(Buffer.concat([t,d])));return Buffer.concat([l,t,d,c]);};
 const head=Buffer.alloc(13);head.writeUInt32BE(w);head.writeUInt32BE(h,4);head[8]=8;head[9]=6;const rows=Buffer.alloc((w*4+1)*h);
 for(let y=0;y<h;y++)rgba.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);
 return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]);}
const atlas=Buffer.alloc(256*16*4);for(let y=0;y<16;y++)for(let x=0;x<256;x++){const i=(y*256+x)*4,c=palette[Math.floor(x/16)];for(let k=0;k<3;k++)atlas[i+k]=c[k];atlas[i+3]=255;}
fs.writeFileSync(path.join(assetDir,'Textures/FourSaintsArmorV3.png'),png(256,16,atlas));

// Offline z-buffer review, NOT a game screenshot. Every visible feature comes from exported mesh triangles.
function triangles(asset,assemble=false){return asset.parts.flatMap(p=>p.tri.map(t=>{
 const offset=assemble?(p.bone==='Leg1'?[-.019845,.711837,-.008183]:p.bone==='Leg2'?[.019847,.711837,-.008183]:[0,0,0]):[0,0,0];
 return {v:t.v.map(v=>add(v,offset)),n:t.n,c:palette[t.m]};}));}
// Read our own crown DAE for the assembled suit preview, not native player geometry.
function crownTriangles(){const dae=fs.readFileSync(path.join(assetDir,'Models/QinglongCrown.dae'),'utf8'),result=[];
 const colors=[[24,38,43],[31,92,100],[54,135,140],[103,183,174],[106,76,43],[178,137,73],[49,161,132],[135,223,185]];
 for(const match of dae.matchAll(/<geometry\b[^>]*>([\s\S]*?)<\/geometry>/g)){
 const text=match[1],read=suffix=>text.match(new RegExp(`<float_array id="[^"]*-${suffix}-array"[^>]*>([^<]+)`))[1].split(/\s+/).map(Number);
 const pos=read('positions'),normal=read('normals'),uv=read('uv'),m=Math.floor(uv[0]*8);
 for(let i=0;i<pos.length;i+=9)result.push({v:[0,3,6].map(k=>add(pos.slice(i+k,i+k+3),[0,1.380558,.023484])),n:normal.slice(i,i+3),c:colors[m]});
 }return result;}
function render(ts,yaw,pitch,white=false){const w=600,h=720,out=Buffer.alloc(w*h*4),depth=new Float64Array(w*h).fill(Infinity);
 for(let i=0;i<w*h;i++){out[i*4]=white?225:24;out[i*4+1]=white?229:32;out[i*4+2]=white?231:39;out[i*4+3]=255;}
 const right=[Math.cos(yaw),0,-Math.sin(yaw)],forward=[Math.sin(yaw)*Math.cos(pitch),-Math.sin(pitch),Math.cos(yaw)*Math.cos(pitch)],up=cross(forward,right);
 const points=ts.flatMap(t=>t.v),px=points.map(v=>dot(v,right)),py=points.map(v=>dot(v,up)),minX=Math.min(...px),maxX=Math.max(...px),minY=Math.min(...py),maxY=Math.max(...py);
 const scale=Math.min(500/(maxX-minX),620/(maxY-minY)),cx=(minX+maxX)/2,cy=(minY+maxY)/2;
 const project=v=>[300+(dot(v,right)-cx)*scale,360-(dot(v,up)-cy)*scale,dot(v,forward)];
 for(const t of ts){const v=t.v.map(project),[a,b,c]=v,area=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);if(Math.abs(area)<1e-8)continue;
 const shade=.48+.52*Math.max(0,dot(t.n,unit([-.4,.8,-.55]))),color=white?[219,225,228]:t.c;
 const x0=Math.max(0,Math.floor(Math.min(...v.map(p=>p[0])))),x1=Math.min(w-1,Math.ceil(Math.max(...v.map(p=>p[0])))),y0=Math.max(0,Math.floor(Math.min(...v.map(p=>p[1])))),y1=Math.min(h-1,Math.ceil(Math.max(...v.map(p=>p[1]))));
 for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){const p=x+.5,q=y+.5,u=((b[0]-p)*(c[1]-q)-(b[1]-q)*(c[0]-p))/area,k=((c[0]-p)*(a[1]-q)-(c[1]-q)*(a[0]-p))/area,j=1-u-k;
 if(u<0||k<0||j<0)continue;const d=u*a[2]+k*b[2]+j*c[2],i=y*w+x;if(d>=depth[i])continue;depth[i]=d;for(let n=0;n<3;n++)out[i*4+n]=Math.round(color[n]*shade);}}
 return png(w,h,out);}
const suit=[...triangles(xuanwu,true),...triangles(baihu,true),...triangles(zhuque,true),...crownTriangles()];
for(const [name,ts] of [['Xuanwu',triangles(xuanwu)],['Baihu',triangles(baihu)],['Zhuque',triangles(zhuque)],['FullSuit',suit]]){
 for(const [view,yaw,pitch] of [['front',0,0],['rear',Math.PI,0],['left',Math.PI/2,0],['right',-Math.PI/2,0],['three-quarter',.65,.15]])fs.writeFileSync(path.join(previewDir,`${name}-${view}.png`),render(ts,yaw,pitch));
 fs.writeFileSync(path.join(previewDir,`${name}-white.png`),render(ts,.65,.15,true));
}
fs.writeFileSync(path.join(previewDir,'summary.json'),JSON.stringify({models:[xuanwu.report,baihu.report,zhuque.report],combinedTriangles:680+xuanwu.report.triangles+baihu.report.triangles+zhuque.report.triangles,nativeMaleLegPivotY:.711837,preview:'Offline authored geometry only; no in-game or Android acceptance implied.'},null,2)+'\n');
