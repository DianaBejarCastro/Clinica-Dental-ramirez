import { afterEach, expect, it, vi } from 'vitest';
import { odontogramApi } from '../modules/doctor/odontogram/api';
import { setSession } from '../services/api';
afterEach(()=>{vi.unstubAllGlobals();setSession(null);});
it('new version sends date and encounter only, without copying findings',async()=>{
 const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify({exam:{id:'new'},findings:[]})));vi.stubGlobal('fetch',fetcher);
 await odontogramApi.version('old','encounter');const [url,init]=fetcher.mock.calls[0];expect(url).toContain('/api/doctor/odontograms/old/new-version');expect(init.method).toBe('POST');expect(Object.keys(JSON.parse(init.body)).sort()).toEqual(['clinicalDate','clinicalEncounterId']);
});
it('draft payload preserves numeric IDs, revision and separate notes',async()=>{
 const fetcher=vi.fn().mockResolvedValue(new Response(null,{status:204}));vi.stubGlobal('fetch',fetcher);
 await odontogramApi.save('exam',{revision:4,dentitionMode:'MIXED',generalNotes:'General',findings:[{id:'local-only',dentalToothId:46,findingTypeId:3,surfaceIds:[2,4],observation:'Local'}]});
 const [,init]=fetcher.mock.calls[0];expect(init.method).toBe('PUT');expect(JSON.parse(init.body)).toEqual({revision:4,dentitionMode:'MIXED',generalNotes:'General',findings:[{dentalToothId:46,findingTypeId:3,surfaceIds:[2,4],observation:'Local'}]});
});
