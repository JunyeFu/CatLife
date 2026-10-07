import test from 'node:test';
import assert from 'node:assert/strict';
import {tasks,stages,evidence} from '../src/data.js';
import {mergeTasks,counts,parseBackup,exportBackup,validateEdit} from '../src/model.js';
test('unique task IDs, known dependencies, no dependency cycles',()=>{
 assert.equal(new Set(tasks.map(t=>t.id)).size,tasks.length);
 for(const t of tasks){assert.ok(stages.some(s=>s.id===t.stage));for(const id of t.deps)assert.ok(tasks.some(t=>t.id===id));}
 const walk=(id,path=[])=>{assert.ok(!path.includes(id));for(const d of tasks.find(t=>t.id===id).deps)walk(d,[...path,id]);};tasks.forEach(t=>walk(t.id));
});
test('counts reflect actual tasks and merge does not change baseline',()=>{
 const t=tasks[1],updated=mergeTasks({[t.id]:{status:'doing'}});assert.equal(updated[1].status,'doing');assert.equal(tasks[1].status,'todo');assert.equal(Object.values(counts(tasks)).reduce((a,b)=>a+b,0),tasks.length);
});
test('done needs evidence and completed prerequisites',()=>{
 const t=tasks.find(t=>t.id==='S01.02');assert.ok(validateEdit({...t,status:'done',proof:''},tasks));assert.equal(validateEdit({...t,status:'done',proof:'实际调研报告'},tasks),'');
 const blocked=tasks.find(t=>t.id==='S03.02');assert.match(validateEdit({...blocked,status:'done',proof:'PRD'},tasks),/前置/);
});
test('backup round trip preserves edits, ignores injected baseline fields',()=>{
 const t=tasks[1],edit={status:'doing',owner:'产品',due:'2026-10-09',proof:'草稿',notes:'继续访谈'};assert.deepEqual(parseBackup(exportBackup({[t.id]:edit})),{[t.id]:edit});
 assert.equal(parseBackup(exportBackup({[t.id]:{...edit,title:'伪造标题'}}))[t.id].title,undefined);
 assert.throws(()=>parseBackup('{broken'));assert.throws(()=>parseBackup(JSON.stringify({schema:'wrong',edits:{}})));
 assert.throws(()=>parseBackup(exportBackup({unknown:edit})));assert.throws(()=>parseBackup(exportBackup({[t.id]:{...edit,status:'done',proof:''}})));
});
test('all done baseline tasks have proof; evidence registry has unique assets',()=>{tasks.filter(t=>t.status==='done').forEach(t=>assert.ok(t.proof));assert.equal(new Set(evidence.map(e=>e.file)).size,evidence.length);});
