'use strict';
const checkDialog=document.querySelector('#voiceCheckDialog');
let checkTimer;

function showCheckPhase(phase,message='',canPlay=false){
  clearInterval(checkTimer);
  const busy=['waiting','capture','original','processed'].includes(phase);
  const playback=phase==='original'||phase==='processed';
  const ready=phase==='ready'||canPlay||playback;
  $('#checkListen').hidden=!ready;
  $('#checkOriginal').disabled=busy;
  $('#checkProcessed').disabled=busy;
  $('#checkAgain').hidden=busy;
  $('#checkRoute').hidden=phase!=='error';
  $('#checkProgress').hidden=phase!=='capture';
  const labels={
    waiting:'Подключаем устройство…',capture:'Говорите в микрофон в течение пяти секунд.',
    original:'Воспроизводится оригинал…',processed:'Воспроизводится голос с эффектом…',
    ready:'Прослушайте одну и ту же фразу в двух вариантах.'
  };
  $('#checkStatus').textContent=message||labels[phase]||'';
  if(phase==='capture'){
    const start=performance.now();
    $('#checkProgress').value=0;
    checkTimer=setInterval(()=>$('#checkProgress').value=Math.min(5,(performance.now()-start)/1000),100);
  }
}

function recordCheck(){
  const route=state.profile.route;
  const output=devices.outputs.find(d=>d.id===route.monitorId)??devices.outputs.find(d=>d.isDefault&&!d.isVirtual);
  $('#checkVoice').textContent=[selectedPreset()?.name,output?.name].filter(Boolean).join(' · ');
  let problem='';
  if(!devices.inputs.some(d=>d.id===route.inputId))problem='Выберите микрофон в разделе «Устройства».';
  else if(!output||output.isVirtual)problem='Выберите физические наушники в разделе «Устройства».';
  else if(route.monitorVolume===0)problem='Громкость прослушивания выставлена на ноль. Прибавьте её в разделе «Устройства».';
  if(problem){showCheckPhase('error',problem);return;}
  showCheckPhase('waiting');
  send('checkCapture');
}

$('#voiceCheckButton').disabled=!native;
$('#voiceCheckButton').addEventListener('click',()=>{checkDialog.showModal();recordCheck();});
$('#checkClose').addEventListener('click',()=>checkDialog.close());
$('#checkAgain').addEventListener('click',recordCheck);
$('#checkRoute').addEventListener('click',()=>{checkDialog.close();showPage('routing');$('#inputDevice').focus();});
for(const action of ['checkOriginal','checkProcessed']){
  $('#'+action).addEventListener('click',()=>{
    showCheckPhase(action==='checkOriginal'?'original':'processed');
    send(action);
  });
}
checkDialog.addEventListener('close',()=>{clearInterval(checkTimer);send('checkClose');});
if(native)window.chrome.webview.addEventListener('message',event=>{
  const {type,data}=event.data;
  if(type!=='voiceCheck'||!checkDialog.open)return;
  if(data.phase==='closed')checkDialog.close();
  else showCheckPhase(data.phase,data.message,data.canPlay);
});
