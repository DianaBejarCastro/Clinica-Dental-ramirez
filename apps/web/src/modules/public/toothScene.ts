import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';

/** Small procedural molar: enamel crown and three tapered roots, no remote assets. */
function molar() {
  const group = new THREE.Group();
  const enamel = new THREE.MeshPhysicalMaterial({ color: '#fff9ec', roughness: .25, metalness: 0, clearcoat: .65, clearcoatRoughness: .2 });
  const vertices:number[] = [], indices:number[] = [];
  const rings = 48, sides = 64;
  for (let i=0;i<=rings;i++) {
    const t=i/rings, angle=t*Math.PI;
    const radius=Math.sin(angle)**.55*(.77+.12*Math.sin(angle));
    for(let j=0;j<=sides;j++) {
      const a=j/sides*Math.PI*2, squircle=1+.07*Math.cos(4*a);
      const cusp=.18*Math.cos(4*a)*Math.sin(angle)**1.3*Math.max(0,Math.cos(angle));
      vertices.push(radius*Math.cos(a)*squircle, .48+.65*Math.cos(angle)+cusp-.28*Math.exp(-((radius/.28)**2))*Math.max(0,Math.cos(angle)), radius*Math.sin(a)*squircle*.9);
      if(i<rings&&j<sides){const k=i*(sides+1)+j;indices.push(k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1);}
    }
  }
  const crown=new THREE.BufferGeometry();crown.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));crown.setIndex(indices);crown.computeVertexNormals();
  group.add(new THREE.Mesh(crown,enamel));
  for(const [x,z,length,bend] of [[-.43,.24,1.7,-.24],[.44,.24,1.55,.23],[.05,-.4,1.45,.12]]) {
    const points=[];
    for(let i=0;i<=32;i++){const t=i/32;points.push(new THREE.Vector2(.31*(1-t)**.65+.008,t*-length));}
    const root=new THREE.LatheGeometry(points.reverse(),32);const pos=root.getAttribute('position');
    for(let i=0;i<pos.count;i++){const t=-pos.getY(i)/length;pos.setX(i,pos.getX(i)+bend*t*t);}
    root.computeVertexNormals();const mesh=new THREE.Mesh(root,enamel);mesh.position.set(x,.27,z);group.add(mesh);
  }
  group.rotation.set(.15,-.35,-.1);return group;
}

export interface ToothScene { rotate(x:number,y:number):void; setPaused(paused:boolean):void; dispose():void }
export function createToothScene(host:HTMLDivElement,onFailure:()=>void):ToothScene {
  const canvas=document.createElement('canvas');
  const context=canvas.getContext('webgl2',{alpha:true,antialias:true});
  if(!context)throw new Error('WebGL unavailable');
  const renderer=new THREE.WebGLRenderer({canvas,context,alpha:true,antialias:true,powerPreference:'low-power'});
  renderer.setPixelRatio(Math.min(window.devicePixelRatio||1,1.5));
  renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;
  canvas.setAttribute('aria-hidden','true');host.append(canvas);
  const scene=new THREE.Scene(),camera=new THREE.PerspectiveCamera(36,1,.1,50);
  camera.position.set(0,1.1,6.5);camera.lookAt(0,-.25,0);
  const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment();
  const environment=pmrem.fromScene(room,.04);scene.environment=environment.texture;room.dispose();pmrem.dispose();
  scene.add(new THREE.HemisphereLight(0xffffff,0x4d7774,1.2));
  const key=new THREE.DirectionalLight(0xffffff,2);key.position.set(-3,4,5);scene.add(key);
  const tooth=molar();scene.add(tooth);
  let paused=false,visible=true,disposed=false,frame=0,last=0,touched=0,targetX=.15,targetY=-.35;
  const motion=matchMedia('(prefers-reduced-motion: reduce)');
  function render(now:number){
    frame=0;if(disposed||!visible||document.hidden)return;
    const dt=Math.min((now-last)/1000,.05);last=now;
    if(!paused&&!motion.matches&&now-touched>2500){targetY+=dt*.16;targetX+=(.15-targetX)*.03;}
    tooth.rotation.y+=(targetY-tooth.rotation.y)*.12;tooth.rotation.x+=(targetX-tooth.rotation.x)*.12;
    renderer.render(scene,camera);
    if((!paused&&!motion.matches)||Math.abs(targetY-tooth.rotation.y)+Math.abs(targetX-tooth.rotation.x)>.001)frame=requestAnimationFrame(render);
  }
  function wake(){if(!frame&&!disposed&&visible&&!document.hidden){last=performance.now();frame=requestAnimationFrame(render);}}
  const size=new ResizeObserver(()=>{const {width,height}=host.getBoundingClientRect();if(width&&height){renderer.setSize(width,height,false);camera.aspect=width/height;camera.updateProjectionMatrix();wake();}});size.observe(host);
  const observer=new IntersectionObserver(([entry])=>{visible=entry.isIntersecting;if(visible)wake();else{cancelAnimationFrame(frame);frame=0;}});observer.observe(host);
  const visibility=()=>wake();document.addEventListener('visibilitychange',visibility);motion.addEventListener('change',wake);
  const lost=(event:Event)=>{event.preventDefault();cancelAnimationFrame(frame);frame=0;onFailure();};canvas.addEventListener('webglcontextlost',lost);
  wake();
  return {rotate(x,y){targetX=THREE.MathUtils.clamp(targetX+x,-.6,.65);targetY+=y;touched=performance.now();wake();},setPaused(value){paused=value;if(value){targetX=tooth.rotation.x;targetY=tooth.rotation.y;}wake();},dispose(){disposed=true;cancelAnimationFrame(frame);size.disconnect();observer.disconnect();document.removeEventListener('visibilitychange',visibility);motion.removeEventListener('change',wake);canvas.removeEventListener('webglcontextlost',lost);tooth.traverse(object=>{if(object instanceof THREE.Mesh)object.geometry.dispose();});(tooth.children[0] as THREE.Mesh<THREE.BufferGeometry,THREE.MeshPhysicalMaterial>).material.dispose();environment.dispose();renderer.dispose();canvas.remove();}};
}
