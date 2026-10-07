async (page) => {
 const check=(ok,text)=>{if(!ok)throw new Error(text);};
 await page.goto('http://127.0.0.1:24186');
 await page.setViewportSize({width:1536,height:1024});
 await page.evaluate(()=>document.fonts.ready);
 const original=await page.evaluate(()=>localStorage.getItem('catlife.lifecycle.v1'));
 try{
  await page.getByRole('button',{name:/S01.02 行业与竞品证据/}).click();
  await page.getByLabel('当前状态').selectOption('done');
  await page.getByRole('button',{name:'保存修改',exact:true}).click();
  check(await page.getByRole('alert').isVisible(),'Missing evidence must prevent done');
  await page.getByLabel('当前状态').selectOption('doing');
  await page.getByLabel('复核备注').fill('浏览器验收临时记录');
  await page.getByLabel('验收证据',{exact:true}).fill('QA proof temporary');
  await page.getByRole('button',{name:'保存修改',exact:true}).click();
  await page.reload();
  await page.getByRole('button',{name:/S01.02 行业与竞品证据/}).click();
  check(await page.getByLabel('当前状态').inputValue()==='doing','Status persists');
  check(await page.getByLabel('复核备注').inputValue()==='浏览器验收临时记录','Notes persist');
  check((await page.locator('.baseline').textContent()).includes('尚无已核验产物'),'Baseline remains immutable');
  await page.getByRole('button',{name:'关闭详情'}).click();
  await page.getByLabel('搜索任务').fill('S07.06');
  check(await page.locator('.task-row').count()===1,'Search filters tasks');
  await page.locator('.task-row').click();
  await page.screenshot({path:'output/task-detail.png',animations:'disabled'});
  await page.getByLabel('当前状态').selectOption('done');
  await page.getByRole('button',{name:'保存修改',exact:true}).click();
  check((await page.getByRole('alert').textContent()).includes('前置'),'Dependency gate');
  await page.keyboard.press('Escape');
  await page.getByLabel('搜索任务').fill('');
  await page.getByLabel('状态筛选').selectOption('decision');
  check(await page.locator('.task-row').count()===5,'Decision filter');
  await page.getByLabel('状态筛选').selectOption('all');
  const download=page.waitForEvent('download');
  await page.getByRole('button',{name:'导出进度',exact:true}).click();
  await (await download).saveAs('output/review-progress.json');
  await page.getByRole('button',{name:'两周计划',exact:true}).click();
  check(await page.locator('.day-plan').count()===14,'14 daily plans');
  await page.screenshot({path:'output/sprint.png'});
  await page.getByRole('button',{name:'知识书架',exact:true}).click();
  check(await page.locator('.book-list article').count()===11,'11 knowledge sources');
  await page.getByLabel('知识主题').selectOption('AI 体验');
  check(await page.locator('.book-list article').count()===2,'Library filter');
  await page.locator('.book-list summary').first().click();
  await page.screenshot({path:'output/library.png'});
  await page.getByRole('button',{name:'验收证据',exact:true}).click();
  check(await page.locator('.evidence-list article').count()===10,'10 evidence items');
  for(const img of await page.locator('.evidence-list img').all()){
   await img.scrollIntoViewIfNeeded();
   await img.evaluate(el=>el.decode());
   check(await img.evaluate(el=>el.naturalWidth>0),'Screenshot loaded');
  }
  const video=page.locator('video').first();
  await video.scrollIntoViewIfNeeded();
  await video.evaluate(async el=>{el.muted=true;await el.play();});
  await page.waitForTimeout(1200);
  check(await video.evaluate(el=>el.currentTime>0&&!el.paused),'Real video plays');
  await video.evaluate(el=>el.pause());
  await page.screenshot({path:'output/evidence.png'});
  await page.setViewportSize({width:390,height:844});
  for(const name of ['生命周期','两周计划','知识书架','验收证据']){
   await page.getByRole('button',{name,exact:true}).click();
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No mobile overflow '+name);
  }
  await page.getByRole('button',{name:'生命周期',exact:true}).click();
  await page.screenshot({path:'output/mobile.png',fullPage:true});
  await page.getByRole('button',{name:/S01.02 行业与竞品证据/}).click();
  await page.screenshot({path:'output/mobile-detail.png',animations:'disabled'});
  await page.keyboard.press('Escape');
  return 'PASS: persistence, immutable baseline, evidence gate, dependency gate, search, filters, export, 14 days, 11 sources, 10 evidence items, real video playback, mobile overflow. Import tested separately via CLI dialog.';
 }finally{
  await page.evaluate(saved=>{if(saved===null)localStorage.removeItem('catlife.lifecycle.v1');else localStorage.setItem('catlife.lifecycle.v1',saved);},original);
  await page.setViewportSize({width:1536,height:1024});
  await page.reload();
  await page.evaluate(()=>document.fonts.ready);
  await page.evaluate(()=>window.scrollTo(0,0));
  await page.screenshot({path:'output/desktop.png'});
  await page.setViewportSize({width:390,height:844});
  await page.evaluate(()=>window.scrollTo(0,0));
  await page.screenshot({path:'output/mobile.png',fullPage:true});
  await page.setViewportSize({width:1536,height:1024});
 }
}
