import React,{useRef,useState} from 'react';
import {createRoot} from 'react-dom/client';
import {tasks as baselineTasks} from './data.js';
import {mergeTasks,storageKey,parseBackup,exportBackup} from './model.js';
import {Icon,Lifecycle,Sprint,Library,Evidence,TaskDrawer} from './components.jsx';
import './style.css';

const pages=[['lifecycle','生命周期','cycle','从产品发现，到发布验收。'],['sprint','两周计划','calendar','10 月 8 日至 21 日，让每一天有明确出口。'],['library','知识书架','book','为行业调研与 PRD，建立可用的知识底稿。'],['evidence','验收证据','file','能证明什么，也说明尚未证明什么。']];
function readInitial(){try{const saved=localStorage.getItem(storageKey);return {edits:saved?parseBackup(saved):{},message:''};}catch(e){return {edits:{},message:`本地进度未载入：${e.message}。原数据未覆盖，请先导出原始备份。`};}}
function App(){
 const [initial]=useState(readInitial),[edits,setEdits]=useState(initial.edits),[page,setPage]=useState('lifecycle'),[taskId,setTaskId]=useState(null),[day,setDay]=useState(null),[message,setMessage]=useState(initial.message);const upload=useRef();
 const tasks=mergeTasks(edits),active=pages.find(p=>p[0]===page);
 const persist=next=>{try{localStorage.setItem(storageKey,exportBackup(next));setEdits(next);setMessage('进度已保存在本机浏览器。');return true;}catch(e){setMessage(`未保存：${e.message}。请导出备份后检查浏览器存储。`);return false;}};
 const download=(content,name)=>{const url=URL.createObjectURL(new Blob([content],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
 const exportData=()=>download(initial.message&&Object.keys(edits).length===0?localStorage.getItem(storageKey)||exportBackup(edits):exportBackup(edits),'CatLife-progress.json');
 const importData=async e=>{const file=e.target.files[0];if(!file)return;try{const next=parseBackup(await file.text());if(window.confirm('导入会替换本机任务更新。先导出的备份可恢复。确认导入？'))persist(next);}catch(err){setMessage(`导入失败：${err.message}`);}e.target.value='';};
 const gotoDay=n=>{setDay(n);setPage('sprint');setTimeout(()=>document.getElementById(`day-${n}`)?.scrollIntoView({behavior:'smooth',block:'start'}),50);};
 return <div className="app-shell"><aside className="sidebar"><div className="brand">CatLife<small>开发工作台</small></div><nav aria-label="主导航">{pages.map(([id,label,icon])=><button key={id} className={page===id?'active':''} onClick={()=>{setPage(id);window.scrollTo(0,0);}} aria-current={page===id?'page':undefined}><Icon name={icon}/>{label}</button>)}</nav><div className="workspace"><div><Icon name="folder"/>本地工作区</div><small>基线 2026.10.08<br/>进度保存在当前浏览器</small><button onClick={()=>upload.current.click()}>导入进度备份</button><input hidden type="file" accept="application/json,.json" ref={upload} onChange={importData}/></div></aside><main><header className="main-header"><div><h1>{active[1]}</h1><p>{active[3]}</p></div><button className="primary" onClick={exportData}>导出进度</button></header>{message&&<div className="notice" role="status"><span>{message}</span><button onClick={()=>setMessage('')} aria-label="关闭消息"><Icon name="close" size={16}/></button></div>}{page==='lifecycle'&&<Lifecycle tasks={tasks} onSelect={setTaskId} onDay={gotoDay}/>} {page==='sprint'&&<Sprint tasks={tasks} selected={day} onSelect={setTaskId}/>} {page==='library'&&<Library/>} {page==='evidence'&&<Evidence/>}<footer className="site-footer"><span>CatLife · 从 MVP 到可提交版本</span><span>阶段框架经本项目裁剪，不代表任何公司的统一流程。</span></footer></main>{taskId&&<TaskDrawer key={taskId} task={{...tasks.find(t=>t.id===taskId),proof:baselineTasks.find(t=>t.id===taskId).proof,...edits[taskId]}} tasks={tasks} onClose={()=>setTaskId(null)} onSave={(id,draft)=>{const next={...edits,[id]:Object.fromEntries(['status','owner','due','proof','notes'].map(k=>[k,draft[k]]))};if(persist(next))setTaskId(null);}}/>}</div>;
}
createRoot(document.getElementById('root')).render(<App/>);
