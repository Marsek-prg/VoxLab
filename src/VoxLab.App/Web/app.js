'use strict';

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
const icons = {
  mic:'<rect x="8" y="2" width="8" height="13" rx="4"/><path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3M8 22h8"/>',
  wave:'<path d="M2 10v4M6 6v12M10 3v18M14 8v8M18 5v14M22 10v4"/>',
  sliders:'<path d="M4 3v8m0 4v6M12 3v3m0 4v11M20 3v12m0 4v2M1 11h6M9 6h6M17 15h6"/>',
  record:'<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3"/>',
  star:'<path d="m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2-5.6-3-5.6 3 1.1-6.2L3 9.6l6.2-.9Z"/>',
  folder:'<path d="M3 7V5a2 2 0 0 1 2-2h5l2 3h7a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z"/>',
  shield:'<path d="m12 2 8 3v6c0 5-4 8-8 11-4-3-8-6-8-11V5Z"/><path d="m8 12 3 3 5-6"/>',
  route:'<rect x="2" y="3" width="7" height="7" rx="2"/><rect x="15" y="14" width="7" height="7" rx="2"/><path d="M5 10v6a2 2 0 0 0 2 2h8M15 6h5m-2-2 2 2-2 2"/>',
  help:'<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 1 1 4 2c-1.3 1-1.5 1.3-1.5 3M12 17h.01"/>',
  minimize:'<path d="M4 15v5h16v-5M12 3v12m-4-4 4 4 4-4"/>',
  download:'<path d="M12 3v12m-4-4 4 4 4-4M4 16v4h16v-4"/>',
  upload:'<path d="M12 16V4m-4 4 4-4 4 4M4 16v4h16v-4"/>',
  search:'<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
  info:'<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7h.01"/>',
  reset:'<path d="M3 10a9 9 0 1 1 2 8M3 4v6h6"/>',
  arrow:'<path d="M3 12h18m-6-6 6 6-6 6"/>',
  headphones:'<path d="M3 13v-1a9 9 0 0 1 18 0v1M3 12h3v9H5a2 2 0 0 1-2-2Zm18 0h-3v9h1a2 2 0 0 0 2-2Z"/>',
  external:'<path d="M14 3h7v7m0-7L10 14M10 3H4v17h17v-6"/>',
  gauge:'<path d="M4 18a9 9 0 1 1 16 0M12 13l5-5M5 18h14"/><circle cx="12" cy="13" r="2"/>',
  code:'<path d="m8 5-6 7 6 7m8-14 6 7-6 7m-3-17-2 20"/>',
  plus:'<path d="M12 4v16M4 12h16"/>',
  play:'<path d="m7 4 14 8-14 8Z"/>',
  stop:'<rect x="5" y="5" width="14" height="14" rx="2"/>',
  power:'<path d="M12 2v9M7 5a9 9 0 1 0 10 0"/>',
  close:'<path d="m6 6 12 12M6 18 18 6"/>',
  check:'<path d="m4 12 5 5L20 6"/>',
  broadcast:'<path d="M3 6a13 13 0 0 1 0 12M7 8a8 8 0 0 1 0 8m14-10a13 13 0 0 0 0 12m-4-10a8 8 0 0 0 0 8M12 3v18"/><circle cx="12" cy="12" r="2"/>',
  feather:'<path d="M4 21 18 7M7 18c-3-10 2-16 12-15 2 11-4 16-12 15Zm3-3 7-1M13 12V6"/>',
  layers:'<path d="m2 8 10 5 10-5-10-5Zm0 5 10 5 10-5M2 18l10 5 10-5"/>',
  spark:'<path d="m12 2 2.5 7.5L22 12l-7.5 2.5L12 22l-2.5-7.5L2 12l7.5-2.5ZM3 3l2 2m14 14 2 2"/>',
  balloon:'<path d="M18 9c0 4-3 7-6 9-3-2-6-5-6-9a6 6 0 0 1 12 0ZM10 19h4m-2 0c-3 2 3 2 0 4M9 8a3 3 0 0 1 3-3"/>',
  clown:'<path d="m4 10 1-6 5 4m4 0 5-4 1 6M5 12a7 7 0 0 0 14 0M8 16q4 4 8 0"/><circle cx="12" cy="12" r="2"/><path d="M7 10h1m8 0h1"/>',
  mountain:'<path d="m1 20 7-13 4 7 4-11 7 17ZM5 13l3 2 3-2m3-4 2 2 2-2"/>',
  robot:'<rect x="4" y="7" width="16" height="14" rx="3"/><path d="M12 3v4M2 11v5m20-5v5M8 16h8M9 16v2m6-2v2"/><circle cx="8" cy="11" r="1"/><circle cx="16" cy="11" r="1"/><circle cx="12" cy="2" r="1"/>',
  cpu:'<rect x="5" y="5" width="14" height="14" rx="3"/><rect x="9" y="9" width="6" height="6" rx="1"/><path d="M8 2v3m8-3v3M8 19v3m8-3v3M2 8h3m-3 8h3M19 8h3m-3 8h3"/>',
  orbit:'<circle cx="12" cy="12" r="3"/><ellipse cx="12" cy="12" rx="11" ry="5" transform="rotate(-35 12 12)"/><path d="M7 3a10 10 0 0 1 14 9M3 12a10 10 0 0 0 14 9"/>',
  ghost:'<path d="M5 21V9a7 7 0 0 1 14 0v12l-4-3-3 3-3-3Z"/><path d="M9 9v2m6-2v2M10 15h4"/>',
  radio:'<rect x="4" y="8" width="16" height="13" rx="2"/><path d="m6 8 13-6M8 17h4m-4-3h4M16 11v7"/><circle cx="16" cy="14" r="1"/>',
  megaphone:'<path d="m3 9 10-2 7-4v18l-7-4-10-2ZM7 16v6h4l-1-5M13 7v10M22 9v6"/>',
  double:'<path d="M3 9v6M7 4v16M11 7v10m4-10v10m4-13v16m4-11v6"/>',
  filter:'<path d="M3 4h18l-7 8v8l-4-2v-6Z"/>',
  mute:'<path d="m3 3 18 18M8 8v3a4 4 0 0 0 7 3M8 4a4 4 0 0 1 8 0v5M5 11a7 7 0 0 0 12 5M12 18v4M8 22h8"/>',
};
const icon = name => `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[name] || icons.wave}</svg>`;
const hydrateIcons = root => $$('[data-icon]',root).forEach(el => { el.innerHTML = icon(el.dataset.icon); });
function rememberFocus(){
  const element=document.activeElement;
  if(!element||element===document.body)return null;
  if(element.id)return {element,selector:'#'+CSS.escape(element.id)};
  for(const key of ['preset','favorite','category','toggle','action','page']){
    if(element.dataset[key])return {element,selector:`[data-${key}="${CSS.escape(element.dataset[key])}"]`};
  }
  return {element};
}
function restoreFocus(saved){
  if(!saved||saved.element.isConnected)return;
  let replacement=saved.selector?$(saved.selector):null;
  if(!replacement||!replacement.getClientRects().length)replacement=$('.tab.active')||$('.preset-select');
  replacement?.focus({preventScroll:true});
}
const escapeHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const colors={sage:'#c5d7a3',sand:'#e3c79b',rose:'#d8aebc',sky:'#a5c9da',peach:'#e8b794',lime:'#c5f277'};
const native = !!window.chrome?.webview;
let state = {profile:{settings:{},route:{},customPresets:[],favorites:[],presetId:'studio',hotkeysEnabled:true},presets:[],running:false,bypass:false,muted:false,recording:false};
let page='voices', category='Все', search='', pendingSettings, busy=false, waveform=Array(128).fill(0), lastRecordingError='';
const parameters = {
  pitch:['Высота голоса',-12,12,.1,'st','Ниже','Выше'], tone:['Тембр',-100,100,1,'%','Теплее','Светлее'],
  inputGain:['Входное усиление',-24,24,.5,'dB'],outputGain:['Уровень выхода',-36,12,.5,'dB'],
  highpassHz:['Срез низких частот',20,300,5,'Hz'],bass:['Низкие · 180 Гц',-12,12,.5,'dB'],mid:['Средние · 1,4 кГц',-12,12,.5,'dB'],treble:['Высокие · 3,6 кГц',-12,12,.5,'dB'],
  gateThreshold:['Порог открытия',-72,-12,1,'dB'],gateAttackMs:['Атака',1,40,1,'ms'],gateHoldMs:['Удержание',10,500,5,'ms'],gateReleaseMs:['Восстановление',20,600,10,'ms'],
  compressorThreshold:['Порог',-48,-3,1,'dB'],compressorRatio:['Соотношение',1,12,.1,':1'],compressorAttackMs:['Атака',1,80,1,'ms'],compressorReleaseMs:['Восстановление',30,700,10,'ms'],compressorMakeup:['Компенсация',0,18,.5,'dB'],
  deEsser:['Сила деэссера',0,100,1,'%'],robotMix:['Роботизация',0,100,1,'%'],robotFrequency:['Частота модуляции',20,180,1,'Hz'],
  echoMix:['Количество эха',0,70,1,'%'],echoDelayMs:['Время повтора',60,900,10,'ms'],echoFeedback:['Обратная связь',0,70,1,'%'],
  reverbMix:['Реверберация',0,70,1,'%'],chorusMix:['Ширина хора',0,100,1,'%'],drive:['Сатурация',0,100,1,'%']
};
const modules=[
  {title:'Характер голоса',icon:'wave',desc:'Высота и окраска — независимо от скорости речи.',keys:['pitch','tone']},
  {title:'Нойзгейт',icon:'filter',toggle:'gateEnabled',desc:'Убирает тихий фон в паузах. Гистерезис 4 dB защищает от дребезга.',keys:['gateThreshold','gateAttackMs','gateHoldMs','gateReleaseMs'],meter:'gate'},
  {title:'Компрессор',icon:'gauge',toggle:'compressorEnabled',desc:'Выравнивает громкость речи. Мягкое колено 6 dB.',keys:['compressorThreshold','compressorRatio','compressorAttackMs','compressorReleaseMs','compressorMakeup'],meter:'compressor'},
  {title:'Эквалайзер',icon:'sliders',desc:'Уберите гул, подчеркните тело и разборчивость.',keys:['highpassHz','bass','mid','treble']},
  {title:'Деэссер',icon:'shield',desc:'Смягчает резкие «с» и «ш» в диапазоне выше 5,2 кГц.',keys:['deEsser']},
  {title:'Робот',icon:'robot',desc:'Кольцевая модуляция для металлических обертонов.',keys:['robotMix','robotFrequency']},
  {title:'Эхо',icon:'wave',desc:'Повторы с управляемой задержкой и обратной связью.',keys:['echoMix','echoDelayMs','echoFeedback']},
  {title:'Пространство',icon:'orbit',desc:'Реверберация комнаты и плавный многослойный хор.',keys:['reverbMix','chorusMix']},
  {title:'Характер и защита',icon:'radio',desc:'Сатурация и радиофильтр. Лимитер всегда защищает выход.',keys:['drive','inputGain','outputGain'],radio:true,limiter:true}
];

function send(action,data=null){
  if(native){window.chrome.webview.postMessage({action,data});return;}
  // This preview never pretends to process microphone audio.
  if(action==='preset'){
    const p=allPresets().find(x=>x.id===data);if(!p)return;
    state.profile.presetId=data;state.profile.settings={...p.settings,inputGain:state.profile.settings.inputGain,outputGain:state.profile.settings.outputGain};renderState();
  }else if(action==='favorite'){
    const f=state.profile.favorites;const i=f.indexOf(data);if(i>=0)f.splice(i,1);else f.push(data);renderGrid();
  }else if(action==='settings'){state.profile.settings=data;updateEdited();}
  else if(action==='savePreset'){
    const preset={id:'user-'+Date.now(),name:data,category:'Мои',description:'Ваши настройки голоса',detail:'Пользовательский пресет.',color:'sage',icon:'sliders',settings:{...state.profile.settings}};
    state.profile.customPresets.push(preset);state.profile.presetId=preset.id;renderState();toast('Пресет создан в текущем предпросмотре. В VoxLab.exe настройки сохраняются на диск.');
  }else if(action==='route'){state.profile.route=data;renderRouting();}
  else if(action==='hotkeys'){state.profile.hotkeysEnabled=data;renderState();}
  else if(action==='monitorVolume'){state.profile.route.monitorVolume=data;}
  else if(action==='deletePreset'){state.profile.customPresets=state.profile.customPresets.filter(p=>p.id!==data);send('preset','studio');}
  else if(action==='bypass'||action==='mute'){toast('Управление звуком доступно в VoxLab.exe.');}
  else toast('Это предпросмотр. Откройте VoxLab.exe, чтобы использовать эту функцию.');
}
function allPresets(){return [...state.presets,...state.profile.customPresets];}
function selectedPreset(){return allPresets().find(p=>p.id===state.profile.presetId)||state.presets[0];}
function format(key,value){
  const p=parameters[key];if(!p)return String(value);
  const unit=p[4];let number=Number(value||0);
  const signed=['pitch','tone','inputGain','outputGain','bass','mid','treble'].includes(key);
  const text=Number.isInteger(number)?String(number):number.toFixed(1);
  return (signed&&number>0?'+':'')+text.replace('-','−')+(unit===':1'?':1':unit==='%'?'%':' '+({st:'st',dB:'dB',Hz:'Гц',ms:'мс'}[unit]||unit));
}
function parameterHtml(key,prefix){
  const [label,min,max,step,,left,right]=parameters[key];const value=state.profile.settings[key]??min;const id=prefix+'-'+key;
  return `<div class="parameter"><div class="parameter-label"><label for="${id}">${label}</label><output for="${id}" data-value="${key}">${format(key,value)}</output></div><input id="${id}" type="range" data-param="${key}" min="${min}" max="${max}" step="${step}" value="${value}" aria-valuetext="${format(key,value)}">${left?`<div class="parameter-range-labels"><span>${left}</span><span>${right}</span></div>`:''}</div>`;
}
function renderCategories(){
  const focus=rememberFocus();
  const names=['Все','Голоса','Персонажи','Студия','Эффекты','Пространство'];
  if(!names.includes(category))names.push(category);
  $('#categories').innerHTML=names.map(c=>`<button class="tab${c===category?' active':''}" data-category="${c}" aria-pressed="${c===category}">${c}</button>`).join('');
  restoreFocus(focus);
}
function renderGrid(){
  const focus=rememberFocus();
  const presets=allPresets().filter(p=>(category==='Все'||category==='Избранное'&&state.profile.favorites.includes(p.id)||p.category===category)&&(`${p.name} ${p.description}`.toLowerCase().includes(search.toLowerCase())));
  $('#presetGrid').innerHTML=presets.map(p=>{
    const selected=p.id===state.profile.presetId;const favorite=state.profile.favorites.includes(p.id);
    return `<article class="preset-card${selected?' selected':''}" style="--preset-color:${colors[p.color]||colors.sage}"><button class="preset-select" data-preset="${escapeHtml(p.id)}" aria-pressed="${selected}" aria-label="${escapeHtml(p.name)}. ${escapeHtml(p.description)}"><span class="preset-symbol">${icon(p.icon)}</span><span class="preset-name">${escapeHtml(p.name)}</span><span class="preset-description">${escapeHtml(p.description)}</span></button><button class="favorite" data-favorite="${escapeHtml(p.id)}" aria-pressed="${favorite}" aria-label="${favorite?'Удалить из избранного':'В избранное'}: ${escapeHtml(p.name)}" title="${favorite?'Удалить из избранного':'В избранное'}">${icon('star')}</button>${selected?`<span class="selected-mark">${icon('check')}</span>`:''}</article>`;
  }).join('');
  $('#emptyPresets').hidden=presets.length>0;
  $('#emptyText').textContent=category==='Мои'?'Настройте голос и нажмите «Сохранить свой пресет».':category==='Избранное'?'Отметьте звёздочкой голоса, к которым хотите вернуться.':'Попробуйте другое название или категорию.';
  $('#customCount').textContent=state.profile.customPresets.length;
  $('.nav-count').textContent=state.presets.length;
  renderCategories();
  restoreFocus(focus);
}
function updateEdited(){
  const preset=selectedPreset();if(!preset)return;
  const edited=Object.keys(preset.settings).some(key=>!['inputGain','outputGain'].includes(key)&&state.profile.settings[key]!==preset.settings[key]);
  $('#editedBadge').hidden=!edited;
}
function renderInspector(){
  const focus=rememberFocus();
  const preset=selectedPreset();if(!preset)return;
  $('#selectedName').textContent=preset.name;$('#selectedDescription').textContent=preset.detail;
  $('#selectedArt').innerHTML=icon(preset.icon);$('#selectedArt').style.color=colors[preset.color]||colors.sage;
  const quick=['pitch','tone','robotMix','echoMix','reverbMix','outputGain'];
  $('#quickParameters').innerHTML=quick.map(k=>parameterHtml(k,'quick')).join('');
  const old=$('#deletePresetButton');if(old)old.remove();
  if(preset.id.startsWith('user-')){
    const button=document.createElement('button');button.className='delete-preset';button.id='deletePresetButton';button.textContent='Удалить этот пресет';button.dataset.action='deletePreset';$('.inspector-bottom').append(button);
  }
  updateEdited();
  restoreFocus(focus);
}
function renderRack(){
  const focus=rememberFocus();
  $('#rackGrid').innerHTML=modules.map((module,i)=>`<section class="rack-module${module.toggle&&!state.profile.settings[module.toggle]?' disabled':''}" data-module="${module.toggle||''}"><div class="module-heading"><div class="module-title"><span>${icon(module.icon)}</span><h2>${module.title}</h2></div>${module.toggle?`<button class="switch" role="switch" aria-checked="${!!state.profile.settings[module.toggle]}" aria-label="${module.title}" data-toggle="${module.toggle}"></button>`:''}</div><p class="module-description">${module.desc}</p><div class="module-parameters">${module.keys.map(k=>parameterHtml(k,'rack')).join('')}</div>${module.radio?`<div class="module-bottom"><span>Радиофильтр · 350–3200 Гц</span><button class="switch" role="switch" aria-checked="${state.profile.settings.radio}" aria-label="Радиофильтр" data-toggle="radio"></button></div>`:''}${module.meter==='gate'?'<div class="module-bottom"><span>Состояние гейта</span><output id="gateState">—</output></div>':''}${module.meter==='compressor'?'<div class="module-bottom"><span>Сжатие сигнала</span><output id="compression">0 dB</output></div>':''}${module.limiter?'<div class="module-bottom"><span>Потолок лимитера</span><output>−0,45 dBFS</output></div>':''}</section>`).join('');
  restoreFocus(focus);
}
let devices={inputs:[],outputs:[]};
function deviceOptions(list,selected,placeholder){return `<option value="">${placeholder}</option>`+list.map(d=>`<option value="${escapeHtml(d.id)}"${d.id===selected?' selected':''}>${escapeHtml(d.name)}${d.isDefault?' · по умолчанию':''}</option>`).join('');}
function renderRouting(){
  const route=state.profile.route;
  $('#inputDevice').innerHTML=deviceOptions(devices.inputs,route.inputId,'Выберите микрофон');
  $('#outputDevice').innerHTML=deviceOptions(devices.outputs,route.outputId,'Выберите выход');
  $('#monitorDevice').innerHTML=deviceOptions(devices.outputs,route.monitorId,'Выберите наушники');
  $('#inputChannel').value=route.inputChannel??-1;$('#latencyMode').value=route.latencyMs??40;
  $('#monitorToggle').setAttribute('aria-checked',String(!!route.monitorEnabled));
  $('#monitorVolume').value=route.monitorVolume??25;$('#monitorVolumeValue').textContent=(route.monitorVolume??25)+'%';
  $$('[data-route]').forEach(el=>el.disabled=state.running||busy);$('#monitorToggle').disabled=state.running||busy;
  $('#routingLock').hidden=!state.running;
  const virtual=devices.outputs.some(d=>d.isVirtual);const notice=$('#cableNotice');
  notice.classList.toggle('warning',!virtual);
  notice.textContent=!native?'В предпросмотре нет доступа к устройствам Windows. Откройте VoxLab.exe.':virtual?'Виртуальный аудиовыход обнаружен. Выберите его ниже, затем укажите парный микрофон в целевом приложении.':'Драйвер виртуального кабеля ещё не обнаружен. Оригинальный архив уже в комплекте; шаги установки ниже. Прослушивание и запись WAV доступны и без драйвера.';
  const input=devices.inputs.find(d=>d.id===route.inputId);const output=devices.outputs.find(d=>d.id===route.outputId);
  $('#routeSummary').textContent=input&&output?shortName(input.name)+' → '+shortName(output.name):'Выберите устройства для начала';
  $('#monitorButton').classList.toggle('active',!!route.monitorEnabled);
}
function shortName(name){return name.length>29?name.slice(0,27)+'…':name;}
function renderState(){
  renderGrid();renderInspector();renderRack();renderRouting();renderTransport();
  $('#hotkeyToggle').setAttribute('aria-checked',String(state.profile.hotkeysEnabled));
  $('#hotkeyWarning').textContent=state.hotkeyFailures?.length?'Заняты другими приложениями: '+state.hotkeyFailures.join(', '):'';
}
function renderTransport(){
  const start=$('#startButton');start.classList.toggle('stopping',state.running);start.disabled=busy||!native;
  start.innerHTML=icon(busy?'gauge':state.running?'stop':'play')+`<span>${busy?'Подключаем…':state.running?'Остановить':'Запустить голос'}</span>`;
  $('#muteButton').setAttribute('aria-pressed',String(state.muted));$('#muteButton').innerHTML=icon(state.muted?'mute':'mic')+`<span>${state.muted?'Без звука':'Микрофон'}</span>`;
  $('#bypassButton').setAttribute('aria-pressed',String(state.bypass));
  $('#engineBadge').classList.toggle('live',state.running&&!state.muted);
  $('#engineBadge').innerHTML='<span class="status-dot"></span><span>'+(!native?'Предпросмотр':state.muted?'Микрофон заглушён':state.running?'Голос в эфире':'Микрофон выключен')+'</span>';
  $('#signalDot').classList.toggle('live',state.running&&!state.muted);
  $('#signalLabel').textContent=state.running?(state.muted?'Выход заглушён':state.bypass?'Исходный голос · FX отключены':'Обработанный сигнал'):'Сигнал на паузе';
  $('#scopeHint').hidden=state.running;
  $('#mainRecordButton').classList.toggle('recording',state.recording);$('#mainRecordButton').innerHTML=icon(state.recording?'stop':'record')+(state.recording?'Завершить запись':'Начать запись');
  $('#footerRecordButton').classList.toggle('recording',state.recording);$('#recordTitle').textContent=state.recording?'Идёт ваш дубль':'Место для вашего голоса';
  $('#mainRecordButton').disabled=!state.running;$('#footerRecordButton').disabled=!state.running;
  if(!state.recording)$('#recordSmallTime').textContent='Запись';
}
function showPage(name){
  if(!$('#page-'+name))return;
  page=name;$('#app').dataset.page=name;
  $$('.page').forEach(el=>{el.hidden=el.id!=='page-'+name;el.classList.toggle('active',!el.hidden);});
  $$('.nav-item[data-page]').forEach(el=>{const active=el.dataset.page===name;el.classList.toggle('active',active);if(active)el.setAttribute('aria-current','page');else el.removeAttribute('aria-current');});
  $('#workspace').scrollTop=0;
  requestAnimationFrame(drawScope);
}
function toast(text,error=false){
  const element=document.createElement('div');element.className='toast'+(error?' error':'');
  const message=document.createElement('span');message.textContent=text;element.append(message);
  const close=document.createElement('button');close.innerHTML=icon('close');close.setAttribute('aria-label','Закрыть уведомление');close.onclick=()=>element.remove();element.append(close);
  $('#toasts').append(element);setTimeout(()=>element.remove(),error?16000:6500);
  while($('#toasts').children.length>3)$('#toasts').firstElementChild.remove();
}
function flushSettings(){clearTimeout(pendingSettings);send('settings',{...state.profile.settings});}
function changeParameter(key,value){
  const [,min,max]=parameters[key];value=Math.max(min,Math.min(max,Number(value)));state.profile.settings[key]=value;
  $$(`[data-param="${key}"]`).forEach(el=>{el.value=value;el.setAttribute('aria-valuetext',format(key,value));});
  $$(`[data-value="${key}"]`).forEach(el=>el.textContent=format(key,value));updateEdited();
  clearTimeout(pendingSettings);pendingSettings=setTimeout(flushSettings,40);
}
function onMeter(data){
  const changed=state.running!==data.running||state.muted!==data.muted||state.bypass!==data.bypass||state.recording!==data.recording;
  state.running=data.running;state.muted=data.muted;state.bypass=data.bypass;state.recording=data.recording;
  if(changed){renderTransport();renderRouting();}
  ['input','output'].forEach(channel=>{
    const db=data[channel+'Db'];$('#'+channel+'Db').textContent=db<=-89?'−∞ dB':db.toFixed(1).replace('-','−')+' dB';
    const meter=$('#'+channel+'Meter');meter.style.transform='scaleX('+Math.max(0,Math.min(1,(db+60)/60))+')';meter.classList.toggle('overload',db>-.5);
  });
  $('#sampleRate').textContent=state.running?(data.sampleRate/1000).toFixed(1).replace('.0','')+' кГц':'— кГц';
  $('#latency').textContent='~ '+(state.running?data.estimatedLatencyMs:'—')+' мс';
  $('#underruns').textContent=data.underruns;$('#dspLoad').textContent=data.dspLoad+'%';
  if($('#gateState'))$('#gateState').textContent=state.running?(data.gateOpen?'Открыт':'Закрыт'):'—';
  if($('#compression'))$('#compression').textContent=(state.running?data.gainReduction.toFixed(1):'0')+' dB';
  if(state.recording){const time=duration(data.recordingSeconds);$('#recordTime').textContent=time;$('#recordSmallTime').textContent=time;}
  if(data.recordingError&&data.recordingError!==lastRecordingError){toast(data.recordingError,true);lastRecordingError=data.recordingError;}
  waveform=data.waveform||waveform;if(page==='voices')drawScope();
}
function duration(seconds){const n=Math.floor(seconds||0);const mins=Math.floor(n/60),secs=n%60;return String(mins).padStart(2,'0')+':'+String(secs).padStart(2,'0');}
function drawScope(){
  const canvas=$('#scope'),rect=canvas.getBoundingClientRect();if(!rect.width)return;
  const dpr=window.devicePixelRatio||1;const width=Math.round(rect.width*dpr),height=Math.round(rect.height*dpr);
  if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
  const ctx=canvas.getContext('2d');ctx.clearRect(0,0,width,height);
  ctx.strokeStyle='#333c31';ctx.lineWidth=dpr;ctx.setLineDash([2*dpr,5*dpr]);ctx.beginPath();ctx.moveTo(0,height/2);ctx.lineTo(width,height/2);ctx.stroke();ctx.setLineDash([]);
  if(!state.running)return;
  ctx.strokeStyle=state.muted?'#85917e':'#c5f277';ctx.lineWidth=1.5*dpr;ctx.beginPath();
  waveform.forEach((v,i)=>{const x=i/(waveform.length-1)*width,y=height/2-Math.max(-1,Math.min(1,v))*height*.43;i?ctx.lineTo(x,y):ctx.moveTo(x,y);});ctx.stroke();
}

document.addEventListener('click',event=>{
  const target=event.target.closest('button,a');if(!target)return;
  if(target.dataset.page){showPage(target.dataset.page);return;}
  if(target.classList.contains('brand')){event.preventDefault();showPage('voices');return;}
  if(target.dataset.preset){clearTimeout(pendingSettings);send('preset',target.dataset.preset);return;}
  if(target.dataset.favorite){send('favorite',target.dataset.favorite);return;}
  if(target.dataset.category){category=target.dataset.category;renderGrid();return;}
  if(target.dataset.toggle){const key=target.dataset.toggle;state.profile.settings[key]=!state.profile.settings[key];renderRack();flushSettings();updateEdited();return;}
  const action=target.dataset.action;if(!action)return;
  if(action==='start'){flushSettings();if(state.running)send('stop');else if(!state.profile.route.inputId||!state.profile.route.outputId){showPage('routing');toast('Выберите микрофон и выход, затем запустите голос.');}else send('start');}
  else if(action==='favorites'||action==='myPresets'){category=action==='favorites'?'Избранное':'Мои';showPage('voices');renderGrid();}
  else if(action==='clearFilter'){category='Все';search='';$('#search').value='';renderGrid();}
  else if(action==='resetPreset'){clearTimeout(pendingSettings);send('preset',state.profile.presetId);}
  else if(action==='saveDialog'){$('#presetName').value='';$('#saveDialog').showModal();$('#presetName').focus();}
  else if(action==='monitorToggle'){send('route',{...state.profile.route,monitorEnabled:!state.profile.route.monitorEnabled});}
  else if(action==='hotkeys'){send('hotkeys',!state.profile.hotkeysEnabled);}
  else if(action==='deletePreset'){send('deletePreset',state.profile.presetId);}
  else{if(['exportPreset','record'].includes(action))flushSettings();send(action);}
});
document.addEventListener('input',event=>{
  const target=event.target;
  if(target.dataset.param)changeParameter(target.dataset.param,target.value);
  if(target.id==='search'){search=target.value;renderGrid();}
  if(target.id==='monitorVolume'){$('#monitorVolumeValue').textContent=target.value+'%';send('monitorVolume',Number(target.value));}
});
document.addEventListener('change',event=>{
  const target=event.target;
  if(target.dataset.route){const key=target.dataset.route;const value=['latencyMs','inputChannel'].includes(key)?Number(target.value):target.value||null;send('route',{...state.profile.route,[key]:value});}
});
$('#saveForm').addEventListener('submit',event=>{
  if(event.submitter?.value==='cancel')return;
  const name=$('#presetName').value.trim();if(!name){event.preventDefault();$('#presetName').focus();return;}
  flushSettings();send('savePreset',name);
});
window.addEventListener('resize',drawScope);
hydrateIcons(document);
$$('.nav-item').forEach(button=>{
  const label=button.textContent.replace(/\d+/g,'').trim();
  button.setAttribute('aria-label',label);button.dataset.tooltip=label;
  const hint=document.createElement('span');hint.className='nav-tooltip';hint.textContent=label;hint.setAttribute('aria-hidden','true');button.append(hint);
});
if(native){
  window.chrome.webview.addEventListener('message',event=>{
    const {type,data}=event.data;
    if(type==='state'){state=data;renderState();}
    else if(type==='devices'){devices=data;renderRouting();}
    else if(type==='meter')onMeter(data);
    else if(type==='notice')toast(data.text,data.error);
    else if(type==='busy'){busy=data;renderTransport();renderRouting();}
  });
  send('ready');
}else{
  document.body.classList.add('preview');$('#previewBanner').hidden=false;
  fetch('presets.json').then(r=>{if(!r.ok)throw new Error('missing');return r.json();}).then(presets=>{
    state.presets=presets;state.profile.settings={...presets[0].settings};state.profile.favorites=['studio','deep','robot','feminine'];state.profile.route={latencyMs:40,monitorVolume:25,inputChannel:-1};renderState();
  }).catch(()=>toast('Нет данных пресетов. Сначала выполните сборку проекта.',true));
}
