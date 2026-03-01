const SOUND_PREF_KEY = "foodtrack_notification_sound";

export type NotificationSoundId =
  | "crystal-drop"
  | "bamboo"
  | "aurora"
  | "dewdrop"
  | "piano-chime"
  | "fahhh";

export type NotificationSound = {
  id: NotificationSoundId;
  name: string;
  description: string;
};

export const NOTIFICATION_SOUNDS: NotificationSound[] = [
  { id: "crystal-drop", name: "Crystal Drop", description: "Sparkling water-drop with shimmer" },
  { id: "bamboo", name: "Bamboo", description: "Warm wooden marimba melody" },
  { id: "aurora", name: "Aurora", description: "Ethereal dreamy glow" },
  { id: "dewdrop", name: "Dewdrop", description: "Delicate harp-like pluck" },
  { id: "piano-chime", name: "Piano Chime", description: "Soft piano chord with sustain" },
  { id: "fahhh", name: "Fahhh", description: "Fun alert tone" },
];

export const DEFAULT_SOUND: NotificationSoundId = "crystal-drop";

let audioCtx: AudioContext | null = null;

function getAudioContext(): AudioContext {
  if (!audioCtx) {
    audioCtx = new AudioContext();
  }
  if (audioCtx.state === "suspended") {
    audioCtx.resume();
  }
  return audioCtx;
}

// -- Layered note helper with proper ADSR envelope --
function playNote(
  ctx: AudioContext,
  freq: number,
  start: number,
  opts: {
    duration?: number;
    type?: OscillatorType;
    volume?: number;
    attack?: number;
    decay?: number;
    sustain?: number;
    release?: number;
    detune?: number;
  } = {},
) {
  const {
    duration = 0.5,
    type = "sine",
    volume = 0.2,
    attack = 0.01,
    decay = 0.1,
    sustain = 0.6,
    release = 0.3,
    detune = 0,
  } = opts;

  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  osc.type = type;
  osc.frequency.setValueAtTime(freq, start);
  if (detune) osc.detune.setValueAtTime(detune, start);

  // ADSR envelope
  const sustainLevel = volume * sustain;
  const sustainEnd = start + duration - release;
  gain.gain.setValueAtTime(0, start);
  gain.gain.linearRampToValueAtTime(volume, start + attack);
  gain.gain.linearRampToValueAtTime(sustainLevel, start + attack + decay);
  gain.gain.setValueAtTime(sustainLevel, Math.max(sustainEnd, start + attack + decay));
  gain.gain.exponentialRampToValueAtTime(0.001, start + duration);

  osc.connect(gain);
  gain.connect(ctx.destination);
  osc.start(start);
  osc.stop(start + duration + 0.05);
}

// -- Shimmer layer: adds harmonic richness --
function addShimmer(ctx: AudioContext, freq: number, start: number, duration: number, vol: number) {
  // Octave above, very quiet for sparkle
  playNote(ctx, freq * 2, start, { duration, volume: vol * 0.15, attack: 0.005, sustain: 0.3, release: duration * 0.6 });
  // Fifth above, even quieter
  playNote(ctx, freq * 3, start + 0.01, { duration: duration * 0.6, volume: vol * 0.07, attack: 0.005, sustain: 0.2, release: duration * 0.4 });
}

// ======================================================
// 1. Crystal Drop - Sparkling water-drop with shimmer
// ======================================================
function playCrystalDrop(ctx: AudioContext) {
  const t = ctx.currentTime;

  // Main drop: high note falling slightly with rich harmonics
  playNote(ctx, 1318, t, { duration: 0.35, volume: 0.2, attack: 0.003, decay: 0.05, sustain: 0.5, release: 0.25 }); // E6
  addShimmer(ctx, 1318, t, 0.35, 0.2);

  // Second drop, lower and warmer
  playNote(ctx, 988, t + 0.18, { duration: 0.45, volume: 0.18, attack: 0.003, decay: 0.08, sustain: 0.4, release: 0.3 }); // B5
  addShimmer(ctx, 988, t + 0.18, 0.45, 0.18);

  // Soft trailing sparkle
  playNote(ctx, 1568, t + 0.3, { duration: 0.5, volume: 0.06, attack: 0.01, decay: 0.1, sustain: 0.3, release: 0.35, type: "triangle" }); // G6
}

// ======================================================
// 2. Bamboo - Warm wooden marimba melody
// ======================================================
function playBamboo(ctx: AudioContext) {
  const t = ctx.currentTime;

  // Marimba-like: sharp attack, fast decay, triangle wave for warmth
  const marimba = (freq: number, start: number, vol: number) => {
    // Fundamental
    playNote(ctx, freq, start, { duration: 0.4, type: "triangle", volume: vol, attack: 0.002, decay: 0.06, sustain: 0.25, release: 0.3 });
    // Soft octave overtone
    playNote(ctx, freq * 2, start, { duration: 0.25, type: "sine", volume: vol * 0.2, attack: 0.002, decay: 0.04, sustain: 0.15, release: 0.2 });
    // Body resonance (slightly detuned for wood character)
    playNote(ctx, freq * 0.998, start, { duration: 0.3, type: "sine", volume: vol * 0.12, attack: 0.002, decay: 0.05, sustain: 0.2, release: 0.2, detune: -5 });
  };

  marimba(523, t, 0.22);         // C5
  marimba(659, t + 0.16, 0.2);   // E5
  marimba(784, t + 0.32, 0.18);  // G5
}

// ======================================================
// 3. Aurora - Ethereal dreamy glow
// ======================================================
function playAurora(ctx: AudioContext) {
  const t = ctx.currentTime;

  // Slow, layered pad-like swells with detuning for width
  const pad = (freq: number, start: number, dur: number, vol: number) => {
    playNote(ctx, freq, start, { duration: dur, type: "sine", volume: vol, attack: 0.15, decay: 0.2, sustain: 0.7, release: dur * 0.4 });
    playNote(ctx, freq, start, { duration: dur, type: "sine", volume: vol * 0.5, attack: 0.18, decay: 0.2, sustain: 0.6, release: dur * 0.4, detune: 7 });
    playNote(ctx, freq, start, { duration: dur, type: "sine", volume: vol * 0.5, attack: 0.18, decay: 0.2, sustain: 0.6, release: dur * 0.4, detune: -7 });
  };

  // Ascending ethereal chord: Am9 voicing
  pad(440, t, 1.2, 0.1);         // A4
  pad(523, t + 0.08, 1.1, 0.09); // C5
  pad(659, t + 0.15, 1.0, 0.08); // E5
  pad(740, t + 0.22, 0.9, 0.06); // F#5 (the 9th for dreamy feel)

  // High airy sparkle
  playNote(ctx, 1319, t + 0.3, { duration: 0.8, type: "sine", volume: 0.03, attack: 0.2, sustain: 0.5, release: 0.4 });
}

// ======================================================
// 4. Dewdrop - Delicate harp-like pluck
// ======================================================
function playDewdrop(ctx: AudioContext) {
  const t = ctx.currentTime;

  // Harp pluck: fast attack, natural decay, layered harmonics
  const harpPluck = (freq: number, start: number, vol: number) => {
    // Fundamental with fast attack
    playNote(ctx, freq, start, { duration: 0.7, type: "sine", volume: vol, attack: 0.002, decay: 0.15, sustain: 0.2, release: 0.5 });
    // 2nd harmonic
    playNote(ctx, freq * 2, start, { duration: 0.4, type: "sine", volume: vol * 0.25, attack: 0.002, decay: 0.08, sustain: 0.15, release: 0.3 });
    // 3rd harmonic, very soft
    playNote(ctx, freq * 3, start, { duration: 0.25, type: "sine", volume: vol * 0.08, attack: 0.002, decay: 0.05, sustain: 0.1, release: 0.18 });
    // Slight detune for natural string width
    playNote(ctx, freq * 1.002, start, { duration: 0.6, type: "sine", volume: vol * 0.3, attack: 0.002, decay: 0.12, sustain: 0.18, release: 0.45, detune: 3 });
  };

  // Descending arpeggio: G5 → E5 → C5 (peaceful descent)
  harpPluck(784, t, 0.2);         // G5
  harpPluck(659, t + 0.2, 0.18);  // E5
  harpPluck(523, t + 0.4, 0.22);  // C5 (resolves warmly)
}

// ======================================================
// 5. Piano Chime - Soft piano chord with sustain
// ======================================================
function playPianoChime(ctx: AudioContext) {
  const t = ctx.currentTime;

  // Piano-like: layered sine + triangle with hammer attack and long sustain
  const pianoKey = (freq: number, start: number, vol: number) => {
    // Main tone
    playNote(ctx, freq, start, { duration: 1.0, type: "sine", volume: vol, attack: 0.005, decay: 0.2, sustain: 0.35, release: 0.6 });
    // Brightness layer
    playNote(ctx, freq, start, { duration: 0.6, type: "triangle", volume: vol * 0.3, attack: 0.003, decay: 0.1, sustain: 0.2, release: 0.4 });
    // 2nd harmonic for richness
    playNote(ctx, freq * 2, start, { duration: 0.5, type: "sine", volume: vol * 0.12, attack: 0.003, decay: 0.08, sustain: 0.15, release: 0.35 });
    // Detuned layer for chorus warmth
    playNote(ctx, freq * 1.001, start, { duration: 0.8, type: "sine", volume: vol * 0.15, attack: 0.005, decay: 0.15, sustain: 0.3, release: 0.5, detune: 4 });
  };

  // Cmaj7 chord, gently rolled
  pianoKey(523, t, 0.14);          // C5
  pianoKey(659, t + 0.06, 0.12);   // E5
  pianoKey(784, t + 0.12, 0.11);   // G5
  pianoKey(988, t + 0.18, 0.09);   // B5 (major 7th for warmth)
}

// ======================================================
// 6. Fahhh - Fun alert tone (MP3)
// ======================================================
function playFahhh() {
  const audio = new Audio("/sounds/fahhh.mp3");
  audio.volume = 0.8;
  audio.play().catch(() => {});
}

// ======================================================

const synthPlayers: Record<string, (ctx: AudioContext) => void> = {
  "crystal-drop": playCrystalDrop,
  "bamboo": playBamboo,
  "aurora": playAurora,
  "dewdrop": playDewdrop,
  "piano-chime": playPianoChime,
};

const mp3Players: Record<string, () => void> = {
  "fahhh": playFahhh,
};

export function playNotificationSound(soundId?: NotificationSoundId) {
  const id = soundId ?? getSelectedSound();
  if (mp3Players[id]) {
    mp3Players[id]();
  } else if (synthPlayers[id]) {
    const ctx = getAudioContext();
    synthPlayers[id](ctx);
  }
}

export function previewSound(soundId: NotificationSoundId) {
  playNotificationSound(soundId);
}

export function getSelectedSound(): NotificationSoundId {
  const stored = localStorage.getItem(SOUND_PREF_KEY);
  if (stored && (synthPlayers[stored as NotificationSoundId] || mp3Players[stored as NotificationSoundId])) {
    return stored as NotificationSoundId;
  }
  return DEFAULT_SOUND;
}

export function setSelectedSound(soundId: NotificationSoundId) {
  localStorage.setItem(SOUND_PREF_KEY, soundId);
}
