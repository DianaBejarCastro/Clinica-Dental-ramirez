import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, render, act } from '@testing-library/react';
import GalleryVideo from '../modules/gallery/GalleryVideo';
import GalleryMedia from '../modules/gallery/GalleryMedia';
import type { MediaItem } from '../modules/gallery/media';
const item = { id:'v', mediaType:'VIDEO_UPLOAD', title:'Recorrido' } as MediaItem;
let callbacks: IntersectionObserverCallback[];
let change: (() => void) | undefined;
let reduced = false;
beforeEach(() => {
 callbacks=[]; reduced=false;
 vi.spyOn(HTMLMediaElement.prototype,'play').mockResolvedValue();
 vi.spyOn(HTMLMediaElement.prototype,'pause').mockImplementation(()=>{});
 vi.stubGlobal('matchMedia',()=>({get matches(){return reduced;},addEventListener:(_type:string,cb:()=>void)=>{change=cb;},removeEventListener:vi.fn()}));
 vi.stubGlobal('IntersectionObserver',class {constructor(cb:IntersectionObserverCallback){callbacks.push(cb);}observe(){}disconnect(){}});
});
afterEach(()=>{cleanup();vi.restoreAllMocks();vi.unstubAllGlobals();});
function visibility(index:number,visible:boolean){act(()=>callbacks[index]([{isIntersecting:visible} as IntersectionObserverEntry],{} as IntersectionObserver));}
it('loads only visible sources, starts muted and pauses offscreen',()=>{
 const {container}=render(<GalleryVideo item={item} full={false} paused={false}/>);const video=container.querySelector('video')!;
 expect(video.getAttribute('src')).toBeNull();visibility(0,true);
 expect(video.src).toContain('/api/clinic/media/v/content');expect(video.muted).toBe(true);expect(video.autoplay).toBe(true);expect(video.loop).toBe(true);expect(video.playsInline).toBe(true);expect(video.preload).toBe('metadata');
 visibility(0,false);expect(video.autoplay).toBe(false);expect(video.getAttribute('src')).toBeNull();expect(video.pause).toHaveBeenCalled();
});
it('limits previews to two and releases a slot when one leaves',()=>{
 const {container}=render(<>{[0,1,2].map(i=><GalleryVideo key={i} item={item} full={false} paused={false}/>)}</>);
 [0,1,2].forEach(i=>visibility(i,true));expect([...container.querySelectorAll('video')].filter(v=>v.autoplay)).toHaveLength(2);
 visibility(0,false);expect(container.querySelectorAll('video')[2].autoplay).toBe(true);
});
it('respects reduced motion and suspends previews for the viewer',()=>{
 reduced=true;const {container,rerender}=render(<GalleryVideo item={item} full={false} paused={false}/>);visibility(0,true);
 const video=container.querySelector('video')!;expect(video.autoplay).toBe(false);expect(video.getAttribute('src')).toBeNull();
 reduced=false;act(()=>change?.());expect(video.autoplay).toBe(true);
 rerender(<GalleryVideo item={item} full={false} paused/>);expect(video.autoplay).toBe(false);
});
it('viewer has controls, never autoplays audio, and stops on close',()=>{
 const {container,unmount}=render(<GalleryVideo item={item} full paused={false}/>);const video=container.querySelector('video')!;
 expect(video.controls).toBe(true);expect(video.autoplay).toBe(false);expect(video.loop).toBe(false);
 unmount();expect(video.pause).toHaveBeenCalled();
});
it('TikTok is branded and never assigned to a video or iframe',()=>{
 const {container}=render(<GalleryMedia item={{...item,mediaType:'TIKTOK',externalUrl:'https://www.tiktok.com/@clinic/video/1234567890123'}}/>);
 expect(container.querySelector('.media-tiktok-badge')?.textContent).toBe('TikTok');expect(container.querySelector('video,iframe')).toBeNull();
});
