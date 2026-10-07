import {tasks,statuses} from './data.js';
export const storageKey='catlife.lifecycle.v1';
export function mergeTasks(edits){return tasks.map(t=>({...t,...edits[t.id]}));}
export function counts(items){return Object.fromEntries(Object.keys(statuses).map(s=>[s,items.filter(t=>t.status===s).length]));}
export function validateEdit(task,all){
 if(task.status==='done'&&!task.proof.trim())return '已验收任务需要填写验收证据。';
 const pending=task.deps.filter(id=>all.find(t=>t.id===id)?.status!=='done');
 if(task.status==='done'&&pending.length)return `前置任务尚未验收：${pending.join('、')}`;
 return '';
}
export function parseBackup(text){
 const data=JSON.parse(text);
 if(data.schema!=='catlife.lifecycle.v1'||!data.edits||Array.isArray(data.edits)||typeof data.edits!=='object')throw new Error('这不是 CatLife 工作台的进度文件。');
 const clean={};
 for(const [id,edit] of Object.entries(data.edits)){
  if(!tasks.some(t=>t.id===id)||!edit||!Object.hasOwn(statuses,edit.status))throw new Error(`未知任务或状态：${id}`);
  for(const k of ['owner','due','proof','notes'])if(typeof edit[k]!=='string')throw new Error(`字段格式错误：${id}.${k}`);
  if(!/^\d{4}-\d{2}-\d{2}$/.test(edit.due)||Number.isNaN(Date.parse(edit.due)))throw new Error(`日期格式错误：${id}`);
  clean[id]={status:edit.status,owner:edit.owner,due:edit.due,proof:edit.proof,notes:edit.notes};
 }
 const merged=mergeTasks(clean);
 for(const [id] of Object.entries(clean)){const err=validateEdit(merged.find(t=>t.id===id),merged);if(err)throw new Error(`${id}：${err}`);}
 return clean;
}
export function exportBackup(edits){return JSON.stringify({schema:'catlife.lifecycle.v1',exportedAt:new Date().toISOString(),edits},null,2);}
