const bootScreen = document.getElementById('boot-screen')
const bootStatus = document.getElementById('boot-status')
const bootPercent = document.getElementById('boot-percent')
const bootFill = document.getElementById('boot-progress-fill')
const bootStages = [
    { at: 18, status: 'INITIALIZING SPATIAL SYSTEMS', check: null },
    { at: 43, status: 'MAPPING ROOM BOUNDARIES', check: 'check-room' },
    { at: 68, status: 'CALIBRATING HAND INPUT', check: 'check-input' },
    { at: 88, status: 'LOADING MISSION PROFILE', check: 'check-mission' },
    { at: 100, status: 'SYSTEMS READY · ENTER WHEN READY', check: null }
]
let bootValue = 0, bootTimer = null, bootDismissed = false
function dismissBoot() {
    if (bootDismissed) return
    bootDismissed = true
    if (bootTimer) clearInterval(bootTimer)
    bootScreen.classList.add('dismissed')
    setTimeout(() => {
        bootScreen.style.display = 'none'
        openPanel('intro')
    }, 750)
}
function updateBoot() {
    if (bootDismissed) return
    bootValue = Math.min(100, bootValue + 1 + Math.floor(Math.random() * 4))
    const stage = [...bootStages].reverse().find(s => bootValue >= s.at) || bootStages[0]
    bootFill.style.width = bootValue + '%'
    bootPercent.textContent = String(bootValue).padStart(2, '0') + '%'
    bootStatus.textContent = stage.status
    bootStages.filter(s => s.check && bootValue >= s.at).forEach(s => document.getElementById(s.check).classList.add('ready'))
}
document.getElementById('boot-skip').addEventListener('click', dismissBoot)
bootTimer = setInterval(updateBoot, 70)

const panels = [...document.querySelectorAll('.panel')]
function openPanel(id) { panels.forEach(p => p.classList.toggle('active', p.id === id)) }
function closePanels() { panels.forEach(p => p.classList.remove('active')) }
document.querySelectorAll('[data-close]').forEach(b => b.addEventListener('click', closePanels))
document.getElementById('start-btn').onclick = () => {
    closePanels()
    showToast('Mission started · room cues active')
}
document.getElementById('inspect-btn').onclick = () => openPanel('inspect')
document.getElementById('resume-btn').onclick = closePanels
let approach = 'quiet', response = 'trust'
document.querySelectorAll('[data-choice]').forEach(el => el.onclick = () => {
    document.querySelectorAll('[data-choice]').forEach(x => x.classList.remove('selected'))
    el.classList.add('selected')
    approach = el.dataset.choice
})
document.getElementById('commit-btn').onclick = () => {
    closePanels()
    document.getElementById('objective-title').textContent = approach === 'quiet' ? 'Relay isolated' : 'Security sweep triggered'
    document.getElementById('objective-copy').textContent = approach === 'quiet' ? 'The relay is contained. A local contact is approaching.' : 'The panel alarm is active. A local contact is approaching.'
    document.getElementById('progress-label').textContent = '02 / 03'
    document.getElementById('progress-bar').style.width = '68%'
    document.getElementById('mission-state').textContent = 'CHOICE RECORDED'
    document.getElementById('inspect-btn').textContent = 'MEET CONTACT'
    document.getElementById('inspect-btn').onclick = () => openPanel('npc')
    const msg = approach === 'quiet' ? 'Approach saved · low profile maintained' : 'Approach saved · security response changed'
    showToast(msg)
}
document.querySelectorAll('[data-response]').forEach(el => el.onclick = () => {
    document.querySelectorAll('[data-response]').forEach(x => x.classList.remove('selected'))
    el.classList.add('selected')
    response = el.dataset.response
})
document.getElementById('response-btn').onclick = () => {
    document.getElementById('summary-choice').textContent = approach === 'quiet' ? 'Quiet access' : 'Forced access'
    document.getElementById('summary-response').textContent = response === 'trust' ? 'Warning accepted' : 'More context requested'
    closePanels()
    document.getElementById('objective-title').textContent = 'Mission choice recorded'
    document.getElementById('objective-copy').textContent = 'Your approach and contact response will influence the next mission beat.'
    document.getElementById('progress-label').textContent = '03 / 03'
    document.getElementById('progress-bar').style.width = '100%'
    document.getElementById('mission-state').textContent = 'SESSION SAVED'
    document.getElementById('inspect-btn').textContent = 'VIEW SESSION LOG'
    document.getElementById('inspect-btn').onclick = () => openPanel('summary')
    showToast('Choice persisted · mission state adapted')
}
document.getElementById('end-btn').onclick = () => openPanel('summary')
document.getElementById('replay-btn').onclick = () => {
    closePanels()
    showToast('Session log saved · room returned to focus')
}

function showToast(message, duration = 2400) {
    const t = document.createElement('div')
    t.classList.add("toast")
    t.innerHTML = message
    toast_area.append(t)
    setTimeout(() => t.remove(), duration)
}

document.body.addEventListener("dblclick", () => showToast(`This is a sample toast`, 5000))

function togglePanel(elem) {
    elem.classList.toggle('active', !elem.classList.contains('active'))
}