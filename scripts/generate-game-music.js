#!/usr/bin/env node
'use strict';

// Cat Metro's original 96 BPM / C-major wooden-toy score. The bundled Apache-2.0
// build-music engine is sample-free. Seeds pin this composition for --check.
const fs = require('fs');
const path = require('path');
const m = require('./vendor/ls-clad/build-music/tools');
const BPM = 96;
const RATE = 44100;
const ROOT = path.resolve(__dirname, '../unity/Assets/Resources/Audio/CatMetro/music');
const output = path.resolve(process.argv[2] || ROOT);

function section(progression, seed, contour, notesPerBar) {
    const { chords, meta } = m.composeChords({ genre: 'folk', key: 'C3', scale: 'major',
        progression, extension: 'none', voicing: 'closeVoiced', voice: 'piano', seed });
    const comp = m.chordEvents(chords, { voice: 'piano', bars: 8,
        velocity: 58, stagger: 0.055, gateOverride: 0.42 });
    const bass = m.composeBass({ chords, genre: 'folk', style: 'root-fifth',
        bars: 8, variation: 0.12, seed: seed + 1 });
    const melody = m.composeMelody({ chords, bars: 8, notesPerBar,
        octaveShift: 2, contour, scale: 'major', scaleRoot: 'C5',
        restProbability: 0.35, seed: seed + 2 });
    // Every fourth bar breathes; only a tiny answering note remains.
    const phrased = melody.filter(e => Math.floor(e.time / 4) % 4 !== 3 || e.time % 4 < 1);
    console.log(JSON.stringify({ harmony: meta, contour, notesPerBar }));
    return { chords, comp, bass, melody: phrased };
}

function offset(events, beats) { return events.map(e => ({ ...e, time: e.time + beats })); }
const a = section(['I','vi','IV','V','I','vi','ii','V'], 27090611, 'arch', 4);
const b = section(['IV','I','ii','V','vi','IV','V','I'], 27090622, 'descend-ascend', 4);
const aprime = section(['I','vi','IV','V','I','vi','ii','V'], 27090633, 'arch', 8);
const c = section(['vi','IV','I','V','IV','ii','V','I'], 27090644, 'falling', 4);
const sections = [a,b,aprime,c];
const collect = key => sections.flatMap((s, i) => offset(s[key], i * 32));
const make = (name, voice, events, fx, seed) => m.track(name, voice, events, {
    seed, fx, humanize: { timeJitter: 0.003, velJitter: 0.035 },
});

const beds = [
    make('felt-comp', 'piano', collect('comp'),
        { hpf: 150, lpf: 2300, reverb: 'smallRoom', gain: 0.48, pan: -0.12 }, 271),
    make('low-pluck', 'nylonGuitar', collect('bass'),
        { hpf: 55, lpf: 950, gain: 0.28, pan: 0.06 }, 272),
];
const melodies = [
    make('music-box', 'musicBox', collect('melody').filter(e => Math.floor(e.time/32)%2 === 0),
        { hpf: 430, lpf: 5800, reverb: 'smallRoom', gain: 0.5, pan: 0.15 }, 273),
    make('kalimba-answer', 'kalimba', collect('melody').filter(e => Math.floor(e.time/32)%2 === 1),
        { hpf: 330, lpf: 4400, reverb: 'smallRoom', gain: 0.6, pan: -0.15 }, 274),
];

// Use the engine's numeric-MIDI drum lanes, then voice them as shaker and a
// muted wooden mallet. The clock stays at eighth notes; accents follow the phrase.
const drums = m.composeDrums({ genre: 'bossa', bars: 32, energy: 0.32,
    fills: false, embellish: 0.12, seed: 27090655 });
console.log(JSON.stringify({ drums: drums.meta, bpm: BPM, form: "A8-B8-A'8-C8" }));
const hatEvents = drums.filter(t => /hat|shaker/.test(t.voice)).flatMap(t => t.events);
const knockEvents = drums.filter(t => /snare|clap/.test(t.voice)).flatMap(t => t.events)
    .filter(e => Math.floor(e.time) % 4 === 2).map(e => ({ ...e, value: 72, beats: 0.1 }));
const percussion = [
    make('shaker', 'shaker', hatEvents.map(e => ({ ...e, value: 70 })),
        { hpf: 2200, lpf: 7600, gain: 0.7, pan: -0.22 }, 275),
    make('woodblock', 'marimba', knockEvents,
        { hpf: 600, lpf: 2200, gain: 0.23, pan: 0.22 }, 276),
];
const sparkleEvents = sections.flatMap((s,i) => {
    const events = m.composeArpeggio({ chords: s.chords, style: 'up-down',
        bars: 8, density: 2, octaveShift: 3, seed: 277 + i });
    return offset(events.filter(e => Math.floor(e.time/4)%2 === 0), i * 32);
});
const sparkles = [make('celebration', 'musicBox', sparkleEvents,
    { hpf: 1100, lpf: 7000, reverb: 'smallRoom', gain: 0.4, pan: 0.1 }, 279)];
const homes = [
    make('home-felt', 'piano', [...a.comp, ...offset(b.comp,32)]
        .filter(e => Math.floor(e.time/4)%2 === 0),
        { hpf: 130, lpf: 2000, reverb: 'smallRoom', gain: 0.55, pan: -0.1 }, 281),
    make('home-tines', 'kalimba', [...a.melody, ...offset(b.melody,32)]
        .filter(e => e.time%8 < 1),
        { hpf: 430, lpf: 3300, reverb: 'smallRoom', gain: 0.21, pan: 0.12 }, 282),
];

function renderLoop(tracks, bars, peakDb) {
    const seconds = bars * 4 * 60 / BPM;
    const count = Math.round(seconds * RATE);
    const raw = m.render(tracks, { bpm: BPM, duration: seconds + 4,
        master: { normalize: 'off', width: 1.05 } });
    const loop = { left: new Float32Array(count), right: new Float32Array(count) };
    let peak = 0;
    for (const side of ['left', 'right']) {
        // Fold natural releases into the next loop instead of cutting them off.
        for (let i=0; i<raw[side].length; i++) loop[side][i%count] += raw[side][i];
        m.audio_primitives.removeDC(loop[side]);
        for (const value of loop[side]) peak = Math.max(peak, Math.abs(value));
    }
    if (!Number.isFinite(peak) || peak < 0.0001) throw new Error('Silent/non-finite stem');
    const gain = Math.pow(10, peakDb/20) / peak;
    for (const side of ['left','right']) for (let i=0;i<count;i++) loop[side][i] *= gain;
    return loop;
}

fs.mkdirSync(output, { recursive: true });
for (const [name, tracks, bars, peak] of [
    ['bed', beds, 32, -9], ['shaker', percussion, 32, -12],
    ['melody', melodies, 32, -12], ['sparkle', sparkles, 32, -16], ['home', homes, 16, -6],
]) m.WavBuilder.write(renderLoop(tracks, bars, peak), path.join(output, name+'.wav'));
