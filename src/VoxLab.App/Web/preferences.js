'use strict';
let hotkeyDraft=null, captureIndex=-1, preferenceSignature='', updateStatus={phase:'idle'};
const actionLabels={mute:'Микрофон: вкл. / выкл.',bypass:'Обход эффектов',engine:'Запуск / остановка',record:'Запись WAV: начать / закончить',window:'Открыть окно',previous:'Предыдущий пресет',next:'Следующий пресет',strengthDown:'Сила эффекта −10%',strengthUp:'Сила эффекта +10%'};
const bindings=()=>hotkeyDraft??state.profile.hotkeyBindings??[];
function editBindings(){if(hotkeyDraft===null)hotkeyDraft=bindings().map(x=>({...x}));return hotkeyDraft;}
function keyLabel(b){return (b.modifiers&2?'Ctrl + ':'')+(b.modifiers&1?'Alt + ':'')+(b.modifiers&4?'Shift + ':'')+(b.key>=112&&b.key<=135?'F'+(b.key-111):b.key===32?'Space':b.key>=37&&b.key<=40?['←','↑','→','↓'][b.key-37]:b.key?String.fromCharCode(b.key):'Назначить сочетание');}
function bindingError(list){
  const seen=new Set();
  for(const b of list){
    const fn=b.key>=112&&b.key<=135;
    if(!b.key)return 'Назначьте клавиши для каждой строки или удалите пустую строку.';
    if(b.key===123||(b.key===115&&(b.modifiers&1))||(!fn&&!(b.modifiers&3)))return 'Нужно сочетание с Ctrl/Alt или F-клавиша. F12 и Alt+F4 зарезервированы Windows.';
    const id=b.key+':'+b.modifiers;if(seen.has(id))return '«'+keyLabel(b)+'» назначено дважды. Измените одно из сочетаний.';seen.add(id);
    if(b.action==='preset'&&!allPresets().some(p=>p.id===b.presetId))return 'Выберите существующий пресет.';
  }
  return '';
}
function renderPreferences(force=false){
  const list=bindings(), signature=JSON.stringify([list,allPresets().map(p=>[p.id,p.name]),captureIndex]);
  if(signature!==preferenceSignature||force){
    const focused=document.activeElement?.id;
    preferenceSignature=signature;
    const actions=Object.entries(actionLabels).map(([id,label])=>({id,label})).concat(allPresets().map(p=>({id:'preset:'+p.id,label:'Голос: '+p.name})));
    $('#hotkeyRows').innerHTML=list.length?list.map((b,i)=>`<div class="hotkey-row"><select id="hotkey-action-${i}" data-key-action="${i}" aria-label="Действие сочетания ${i+1}">${actions.map(a=>`<option value="${escapeHtml(a.id)}"${a.id===(b.action==='preset'?'preset:'+b.presetId:b.action)?' selected':''}>${escapeHtml(a.label)}</option>`).join('')}</select><button id="hotkey-key-${i}" class="button secondary hotkey-key${captureIndex===i?' capturing':''}${!b.key?' invalid':''}" data-key-capture="${i}" aria-label="Сочетание ${i+1}: ${escapeHtml(keyLabel(b))}">${captureIndex===i?'Нажмите клавиши…':escapeHtml(keyLabel(b))}</button><button class="icon-button" id="hotkey-remove-${i}" data-key-remove="${i}" aria-label="Удалить сочетание ${i+1}" title="Удалить сочетание">${icon('close')}</button></div>`).join(''):'<p class="helper hotkey-empty">Сочетаний пока нет. Добавьте первое ниже.</p>';
    if(focused)document.getElementById(focused)?.focus({preventScroll:true});
  }
  const error=bindingError(list);$('#hotkeyError').textContent=error;
  $('#saveHotkeys').disabled=!!error||captureIndex>=0||hotkeyDraft===null;
  $('#addHotkey').disabled=list.length>=64||captureIndex>=0;
  $('#hotkeyDirty').hidden=hotkeyDraft===null;
  renderUpdate(updateStatus);
}
function cancelKeyCapture(){
  if(captureIndex<0)return;
  captureIndex=-1;send('captureHotkey',false);renderPreferences();
}
function renderUpdate(status){
  updateStatus=status;
  if(status.message)$('#updateMessage').textContent=status.message;
  $('.update-panel').dataset.phase=status.phase;
  $('#checkUpdate').disabled=['checking','downloading','installing'].includes(status.phase);
  $('#downloadUpdate').hidden=status.phase!=='available';
  $('#installUpdate').hidden=status.phase!=='ready';
  $('#installUpdate').disabled=state.recording||busy;
  $('#updateSize').textContent=status.downloadBytes?`${(status.downloadBytes/1024/1024).toFixed(1)} МБ · файлов к замене: ${status.changedFiles}`:'';
  if(status.phase==='ready'&&state.recording)$('#updateSize').textContent='Сначала завершите запись WAV.';
}
document.addEventListener('click',event=>{
  const button=event.target.closest('button');if(!button)return;
  if(button.dataset.keyCapture!==undefined){
    const index=Number(button.dataset.keyCapture);
    cancelKeyCapture();editBindings();captureIndex=index;send('captureHotkey',true);renderPreferences();
    $('#hotkey-key-'+index)?.focus();
  }else if(button.dataset.keyRemove!==undefined){
    cancelKeyCapture();const index=Number(button.dataset.keyRemove);editBindings().splice(index,1);renderPreferences();
    ($('#hotkey-key-'+Math.min(index,bindings().length-1))||$('#addHotkey')).focus();
  }else if(button.id==='addHotkey'){
    editBindings().push({key:0,modifiers:3,action:'mute',presetId:null});renderPreferences();$('#hotkey-action-'+(bindings().length-1)).focus();
  }else if(button.id==='saveHotkeys'){
    if(!bindingError(bindings()))send('saveHotkeys',bindings().map(({key,modifiers,action,presetId})=>({key,modifiers,action,presetId})));
  }else if(button.id==='resetHotkeys'){
    cancelKeyCapture();send('resetHotkeys');
  }
});
document.addEventListener('change',event=>{
  if(event.target.dataset.keyAction===undefined)return;
  cancelKeyCapture();const i=Number(event.target.dataset.keyAction),value=event.target.value;
  editBindings()[i]={...bindings()[i],action:value.startsWith('preset:')?'preset':value,presetId:value.startsWith('preset:')?value.slice(7):null};renderPreferences();
});
document.addEventListener('keydown',event=>{
  if(captureIndex<0)return;
  if(event.key==='Escape'||event.key==='Tab'){cancelKeyCapture();if(event.key==='Escape')event.preventDefault();return;}
  event.preventDefault();event.stopPropagation();
  if(event.repeat||['Control','Alt','Shift','Meta','AltGraph'].includes(event.key))return;
  let key=0;
  if(/^Key[A-Z]$/.test(event.code))key=event.code.charCodeAt(3);
  else if(/^Digit[0-9]$/.test(event.code))key=event.code.charCodeAt(5);
  else if(/^F([1-9]|1[0-9]|2[0-4])$/.test(event.code))key=111+Number(event.code.slice(1));
  else key={Space:32,ArrowLeft:37,ArrowUp:38,ArrowRight:39,ArrowDown:40}[event.code]||0;
  const modifiers=(event.ctrlKey?2:0)|(event.altKey?1:0)|(event.shiftKey?4:0);
  if(!key||event.metaKey){$('#hotkeyError').textContent='Используйте Ctrl/Alt + букву, цифру, стрелку, Space или F-клавишу.';return;}
  const index=captureIndex;editBindings()[index]={...bindings()[index],key,modifiers};cancelKeyCapture();$('#hotkey-key-'+index)?.focus();
},true);
window.addEventListener('blur',cancelKeyCapture);
