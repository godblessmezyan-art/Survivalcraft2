// Original, deterministic low-poly geometry. Run: node New-QinglongCrownAssets.mjs
// No external art, libraries, or game files. Model coordinates: +Y up, -Z face.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateSync } from 'node:zlib';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const parts = [];
const palette = [[24,38,43],[31,92,100],[54,135,140],[103,183,174],
  [106,76,43],[178,137,73],[49,161,132],[135,223,185]];
const add=(a,b)=>a.map((v,i)=>v+b[i]), sub=(a,b)=>a.map((v,i)=>v-b[i]);
const mul=(a,k)=>a.map(v=>v*k), dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const unit=a=>mul(a,1/Math.hypot(...a));
function part(name, material) { const p={name,material,tri:[]};parts.push(p);return p; }
function tri(p,a,b,c,inside) {
  let n=cross(sub(b,a),sub(c,a));
  if(Math.hypot(...n)<1e-10) throw Error('Degenerate face: '+p.name);
  if(dot(n,sub(mul(add(add(a,b),c),1/3),inside))<0){[b,c]=[c,b];n=mul(n,-1);}
  p.tri.push({v:[a,b,c],n:unit(n),m:p.material});
}
function quad(p,a,b,c,d,inside){tri(p,a,b,c,inside);tri(p,a,c,d,inside);}
// Convex outline extruded through Z, with a raised faceted central ridge.
function plate(name,outline,z,depth,material,ridge=0) {
  const p=part(name,material), centre=[outline.reduce((s,v)=>s+v[0],0)/outline.length,
    outline.reduce((s,v)=>s+v[1],0)/outline.length,z+depth/2];
  const front=outline.map(([x,y])=>[x,y,z]), back=outline.map(([x,y])=>[x,y,z+depth]);
  for(let i=0;i<outline.length;i++) {
    let j=(i+1)%outline.length;
    tri(p,[centre[0],centre[1],z-ridge],front[i],front[j],centre);
    tri(p,[centre[0],centre[1],z+depth],back[j],back[i],centre);
    quad(p,front[i],back[i],back[j],front[j],centre);
  }
}
function tube(name,points,radii,material,sides=6) {
  const p=part(name,material), rings=[];
  for(let k=0;k<points.length;k++) {
    const axis=unit(sub(points[Math.min(k+1,points.length-1)],points[Math.max(0,k-1)]));
    const e=unit(cross(Math.abs(axis[1])<.9?[0,1,0]:[1,0,0],axis)), f=cross(axis,e);
    rings.push(Array.from({length:sides},(_,i)=>add(points[k],add(mul(e,Math.cos(i*2*Math.PI/sides)*radii[k]),mul(f,Math.sin(i*2*Math.PI/sides)*radii[k])))));
  }
  for(let k=0;k<points.length-1;k++)for(let i=0;i<sides;i++) {
    const j=(i+1)%sides, centre=mul(add(points[k],points[k+1]),.5);
    quad(p,rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i],centre);
  }
  for(let i=0;i<sides;i++) {
    tri(p,points[0],rings[0][(i+1)%sides],rings[0][i],points[1]);
    let k=points.length-1;
    tri(p,points[k],rings[k][i],rings[k][(i+1)%sides],points[k-1]);
  }
}
// Hollow circlet: no hidden solid helmet intersecting the face or head.
for(let i=0;i<12;i++) {
  const a=2*Math.PI*i/12,b=2*Math.PI*(i+1)/12;
  const ring=(ang,rx,rz,y)=>[Math.sin(ang)*rx,y,Math.cos(ang)*rz];
  const p=part('Circlet'+i,i%3===0?4:1);
  const o=[ring(a,.194,.187,.242),ring(b,.194,.187,.242),ring(b,.194,.187,.309),ring(a,.194,.187,.309)];
  const n=[ring(a,.179,.172,.242),ring(b,.179,.172,.242),ring(b,.179,.172,.309),ring(a,.179,.172,.309)];
  const c=mul(add(add(o[0],o[2]),add(n[0],n[2])),.25);
  quad(p,...o,c);quad(p,n[3],n[2],n[1],n[0],c);
  quad(p,o[0],n[0],n[1],o[1],c);quad(p,o[3],o[2],n[2],n[3],c);
  quad(p,o[0],o[3],n[3],n[0],c);quad(p,o[1],n[1],n[2],o[2],c);
}
for(const s of [-1,1]) {
  tube(s<0?'HornLeft':'HornRight',[[s*.147,.302,.005],[s*.181,.373,.041],[s*.202,.452,.108],[s*.218,.518,.185],[s*.225,.556,.247]],[.042,.035,.026,.016,.0025],5);
  tube(s<0?'HornSocketLeft':'HornSocketRight',[[s*.147,.289,.005],[s*.159,.331,.020]],[.048,.041],4,8);
  tube(s<0?'WhiskerLeft':'WhiskerRight',[[s*.182,.264,-.116],[s*.205,.204,-.128],[s*.218,.151,-.089],[s*.237,.172,-.027]],[.023,.020,.012,.0025],2);
  const outline=[[.045,.277],[.091,.315],[.160,.328],[.187,.291],[.151,.275],[.096,.291]].map(([x,y])=>[s*x,y]);
  plate(s<0?'DragonBrowLeft':'DragonBrowRight',outline,-.192,.023,2,.018);
  plate(s<0?'BronzeTempleLeft':'BronzeTempleRight',[[s*.151,.252],[s*.178,.287],[s*.196,.255],[s*.177,.204]],-.151,.026,5,.006);
}
plate('ForeheadScale',[[0,.386],[.054,.326],[.043,.278],[0,.253],[-.043,.278],[-.054,.326]],-.203,.032,5,.012);
plate('JadeHeart',[[0,.362],[.027,.326],[0,.290],[-.027,.326]],-.226,.016,6,.020);
plate('JadeHighlight',[[0,.350],[.010,.327],[0,.314],[-.009,.327]],-.249,.006,7,.005);
for(let i=0;i<3;i++) {
  // Rear segmented crest, with real thickness and a short swept fin.
  const y=.294-i*.047,z=.164+i*.006;
  plate('NapeScale'+i,[[-.060,y],[0,y+.052],[.060,y],[.045,y-.032],[0,y-.048],[-.045,y-.032]],z,.025,i===1?2:1,.009);
  tube('NapeFin'+i,[[0,y+.014,z+.021],[0,y+.033,z+.078]],[.018,.0025],4,4);
}

const fmt=n=>Number(n.toFixed(6)).toString();
const join=a=>a.map(fmt).join(' ');
function source(id,data,stride,params) {
  return `<source id="${id}"><float_array id="${id}-array" count="${data.length}">${join(data)}</float_array><technique_common><accessor source="#${id}-array" count="${data.length/stride}" stride="${stride}">${params.map(n=>`<param name="${n}" type="float"/>`).join('')}</accessor></technique_common></source>`;
}
const geometries=parts.map(p=>{
  const id=p.name, pos=p.tri.flatMap(t=>t.v.flat()),norm=p.tri.flatMap(t=>[...t.n,...t.n,...t.n]);
  // Interior texel of an eight-swatch atlas: flat facets carry the silhouette.
  const u=(p.material*16+8)/128,uv=p.tri.flatMap(()=>[u,.5,u,.5,u,.5]);
  const indices=Array.from({length:pos.length/3},(_,i)=>`${i} ${i} ${i}`).join(' ');
  return `<geometry id="${id}-mesh" name="${id}"><mesh>${source(id+'-positions',pos,3,['X','Y','Z'])}${source(id+'-normals',norm,3,['X','Y','Z'])}${source(id+'-uv',uv,2,['S','T'])}<vertices id="${id}-vertices"><input semantic="POSITION" source="#${id}-positions"/></vertices><triangles count="${p.tri.length}"><input semantic="VERTEX" source="#${id}-vertices" offset="0"/><input semantic="NORMAL" source="#${id}-normals" offset="1"/><input semantic="TEXCOORD" source="#${id}-uv" offset="2" set="0"/><p>${indices}</p></triangles></mesh></geometry>`;
});
const dae=`<?xml version="1.0" encoding="utf-8"?>\n<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1"><asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-QinglongCrownAssets.mjs</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset><library_geometries>${geometries.join('\n')}</library_geometries><library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="QinglongCrown" name="QinglongCrown" type="NODE"><matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>${parts.map(p=>`<instance_geometry url="#${p.name}-mesh"/>`).join('')}</node></visual_scene></library_visual_scenes><scene><instance_visual_scene url="#Scene"/></scene></COLLADA>\n`;

function crc32(data){let c=0xffffffff;for(const b of data){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function png(w,h,rgba){
  const chunk=(type,d)=>{const t=Buffer.from(type),l=Buffer.alloc(4),c=Buffer.alloc(4);l.writeUInt32BE(d.length);c.writeUInt32BE(crc32(Buffer.concat([t,d])));return Buffer.concat([l,t,d,c]);};
  const head=Buffer.alloc(13);head.writeUInt32BE(w);head.writeUInt32BE(h,4);head[8]=8;head[9]=6;
  const rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)rgba.copy(rows,y*(w*4+1)+1,y*w*4,(y+1)*w*4);
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]);
}
const atlas=Buffer.alloc(128*16*4);for(let y=0;y<16;y++)for(let x=0;x<128;x++){
  const c=palette[Math.floor(x/16)],i=(y*128+x)*4;for(let k=0;k<3;k++)atlas[i+k]=c[k];atlas[i+3]=255;
}
const modelPath=path.join(root,'src/SCIEPort/Assets/Models/QinglongCrown.dae');
const texturePath=path.join(root,'src/SCIEPort/Assets/Textures/QinglongCrown.png');
fs.writeFileSync(modelPath,dae);fs.writeFileSync(texturePath,png(128,16,atlas));

// Z-buffered orthographic previews from the SAME authored triangle stream.
// The grey head is an explicitly approximate calibration proxy, never exported in DAE.
function render(yaw,pitch,white=false,head=false) {
  const w=600,h=600,out=Buffer.alloc(w*h*4),depth=new Float64Array(w*h).fill(Infinity);
  for(let i=0;i<w*h;i++){out[i*4]=white?225:24;out[i*4+1]=white?229:32;out[i*4+2]=white?231:39;out[i*4+3]=255;}
  const triangles=parts.flatMap(p=>p.tri);
  if(head){
    const x=.172,y0=0,y1=.318,z=.161,pts=[[-x,y0,-z],[x,y0,-z],[x,y1,-z],[-x,y1,-z],[-x,y0,z],[x,y0,z],[x,y1,z],[-x,y1,z]];
    for(const ids of [[0,1,2,3],[1,5,6,2],[5,4,7,6],[4,0,3,7],[3,2,6,7],[4,5,1,0]])for(const t of [[0,1,2],[0,2,3]]){
      const v=t.map(i=>pts[ids[i]]);triangles.push({v,n:unit(cross(sub(v[1],v[0]),sub(v[2],v[0]))),m:8});
    }
  }
  const right=[Math.cos(yaw),0,-Math.sin(yaw)],forward=[Math.sin(yaw)*Math.cos(pitch),-Math.sin(pitch),Math.cos(yaw)*Math.cos(pitch)],up=cross(forward,right);
  const project=v=>[300+dot(v,right)*740,320-dot(sub(v,[0,.29,0]),up)*740,dot(v,forward)];
  for(const t of triangles){
    const v=t.v.map(project),[a,b,c]=v,area=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);if(Math.abs(area)<1e-8)continue;
    const shade=.45+.55*Math.max(0,dot(t.n,unit([-.4,.8,-.55]))),col=t.m===8?[105,111,117]:white?[215,219,222]:palette[t.m];
    for(let y=Math.max(0,Math.floor(Math.min(...v.map(p=>p[1]))));y<=Math.min(h-1,Math.ceil(Math.max(...v.map(p=>p[1]))));y++)for(let x=Math.max(0,Math.floor(Math.min(...v.map(p=>p[0]))));x<=Math.min(w-1,Math.ceil(Math.max(...v.map(p=>p[0]))));x++){
      const p=x+.5,q=y+.5,u=((b[0]-p)*(c[1]-q)-(b[1]-q)*(c[0]-p))/area,k=((c[0]-p)*(a[1]-q)-(c[1]-q)*(a[0]-p))/area,j=1-u-k;
      if(u<0||k<0||j<0)continue;const d=u*a[2]+k*b[2]+j*c[2],i=y*w+x;if(d>=depth[i])continue;depth[i]=d;
      for(let n=0;n<3;n++)out[i*4+n]=Math.round(col[n]*shade);
    }
  }
  return png(w,h,out);
}
const preview=path.join(root,'docs/assets/qinglong-v3');fs.mkdirSync(preview,{recursive:true});
for(const [name,yaw,pitch] of [['front',0,0],['rear',Math.PI,0],['left',Math.PI/2,0],['right',-Math.PI/2,0],['three-quarter',.65,.2],['top',0,1.3]]){
  fs.writeFileSync(path.join(preview,name+'.png'),render(yaw,pitch));
  fs.writeFileSync(path.join(preview,name+'-fit.png'),render(yaw,pitch,false,true));
  if(name==='three-quarter')fs.writeFileSync(path.join(preview,'white.png'),render(yaw,pitch,true));
}
const points=parts.flatMap(p=>p.tri.flatMap(t=>t.v));
const report={generator:'New-QinglongCrownAssets.mjs',bones:1,meshes:parts.length,triangles:parts.reduce((s,p)=>s+p.tri.length,0),
  bounds:[0,1,2].map(i=>[Math.min(...points.map(v=>v[i])),Math.max(...points.map(v=>v[i]))]),
  parts:parts.map(p=>({name:p.name,triangles:p.tri.length,material:p.material})),
  calibration:'Provisional head-local coordinates. Grey fit proxy is NOT an extracted game head; target male/female model and in-game poses must be checked.'};
fs.writeFileSync(path.join(preview,'geometry.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report));
