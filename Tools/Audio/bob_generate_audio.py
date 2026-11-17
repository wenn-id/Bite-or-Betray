"""Generate original soundtrack and sound effects (.wav) for Bite or Betray.

    python Tools/Audio/bob_generate_audio.py

Outputs 14 studio-quality 44.1 kHz 16-bit stereo WAV files into Assets/Resources/Audio/:
  - BoB_Music.wav        (Round 1 & Menu: 32.0s Playful Parlour Swing in C Minor, 120 BPM)
  - BoB_Music_Round2.wav (Round 2: 30.5s Sly Habanera / Parlour Tango in D Minor, 126 BPM)
  - BoB_Music_Round3.wav (Round 3: 28.7s Speakeasy Detective Hustle in F Minor, 134 BPM)
  - BoB_Music_Round4.wav (Round 4+: 27.0s High-Stakes Showdown Swing in G Minor, 142 BPM)
  - BoB_Pickup.wav       (Candy pickup ceramic clink + pop)
  - BoB_Give.wav        (Slide whoosh + playful offer chime)
  - BoB_EatGood.wav     (Crisp candy bite + 3 chew crunches + sparkly Yummy celesta arpeggio)
  - BoB_EatBad.wav      (Candy bite + sour fizzy chew + queasy descending wah-wah poison cue)
  - BoB_UseItem.wav     (Tactile gadget/item activation + arcane shimmer)
  - BoB_Damage.wav      (Sub-bass heart-loss impact + porcelain crack)
  - BoB_Elimination.wav (Dramatic parlour organ/brass elimination stinger)
  - BoB_Win.wav         (Upbeat swing brass & vibraphone victory fanfare)
  - BoB_Waiter.wav      (Silver service bell ding + cloche dome lift whoosh)
  - BoB_MenuSelect.wav  (Crisp wooden mallet UI tap)
"""
import math
import os
import wave
import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Audio")


def midi_to_freq(m):
    return 440.0 * (2.0 ** ((m - 69.0) / 12.0))


def pan_stereo(mono, pan=0.0):
    """Constant-power stereo pan (-1.0 = left, 0.0 = center, +1.0 = right)."""
    angle = (np.clip(pan, -1.0, 1.0) + 1.0) * (math.pi / 4.0)
    left = mono * math.cos(angle)
    right = mono * math.sin(angle)
    return np.stack([left, right], axis=-1)


def add_stereo(buf, clip_st, start_sec):
    i0 = int(round(start_sec * SR))
    if i0 >= len(buf):
        return
    n = min(len(clip_st), len(buf) - i0)
    if n > 0:
        buf[i0:i0 + n] += clip_st[:n]


def add_Wrapped(buf, clip_st, start_sec):
    """Add stereo clip into circular loop buffer so reverb/release tails wrap seamlessly."""
    total = len(buf)
    i0 = int(round(start_sec * SR)) % total
    n = len(clip_st)
    if i0 + n <= total:
        buf[i0:i0 + n] += clip_st
    else:
        n1 = total - i0
        buf[i0:] += clip_st[:n1]
        buf[:n - n1] += clip_st[n1:]


def simple_parlour_reverb(stereo, wet=0.18, wrap=False):
    """Warm stereo Schroeder/comb parlour room reverb."""
    delays_ms = [(29.7, -0.3), (37.1, 0.3), (43.3, -0.5), (53.9, 0.5), (67.3, -0.2)]
    gains = [0.42, 0.34, 0.28, 0.22, 0.16]
    out = stereo.copy()
    wet_buf = np.zeros_like(stereo)
    n = len(stereo)
    for (dms, xfeed), g in zip(delays_ms, gains):
        d = int(dms * 0.001 * SR)
        if wrap:
            shifted = np.roll(stereo, d, axis=0)
        else:
            shifted = np.zeros_like(stereo)
            if d < n:
                shifted[d:] = stereo[:-d]
        # Cross-mix L/R slightly for stereo width
        l = shifted[:, 0] + xfeed * shifted[:, 1]
        r = shifted[:, 1] - xfeed * shifted[:, 0]
        wet_buf[:, 0] += g * l
        wet_buf[:, 1] += g * r
    return out * (1.0 - wet * 0.35) + wet_buf * wet


def save_wav(filename, stereo, normalize_peak=0.92):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, filename)
    peak = np.max(np.abs(stereo))
    if peak > 1e-6:
        stereo = stereo * (normalize_peak / peak)
    stereo = np.clip(stereo, -1.0, 1.0)
    # Reach silence at clip boundaries so truncated reverb and loop seams do not click.
    fade_frames = min(int(SR * 0.005), len(stereo) // 2)
    if fade_frames:
        fade = np.linspace(0.0, 1.0, fade_frames)[:, None]
        stereo[:fade_frames] *= fade
        stereo[-fade_frames:] *= fade[::-1]
    pcm = (stereo * 32767.0).astype(np.int16)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())
    print(f"[audio] saved {path} ({len(stereo)/SR:.2f}s)")


# ==============================================================================
# INSTRUMENT SYNTHESIZERS FOR PARLOUR JAZZ / SWING SOUNDTRACK
# ==============================================================================

def synth_upright_bass(midi_note, dur, vel=1.0):
    n = int( (dur + 0.08) * SR )
    t = np.arange(n) / SR
    f = midi_to_freq(midi_note)
    # Slight pitch drop on pluck attack for woody acoustic bass feel
    f_inst = f * (1.0 + 0.018 * np.exp(-t / 0.018))
    phase = 2.0 * np.pi * np.cumsum(f_inst) / SR
    # Warm fundamental + plucked string harmonics with faster high-harmonic decay
    sig = (
        0.70 * np.sin(phase)
        + 0.38 * np.sin(2.0 * phase) * np.exp(-t / 0.18)
        + 0.18 * np.sin(3.0 * phase) * np.exp(-t / 0.10)
        + 0.08 * np.sin(4.0 * phase) * np.exp(-t / 0.05)
    )
    # Finger pluck transient
    rng = np.random.default_rng(int(midi_note * 100 + dur * 1000))
    click = rng.normal(0, 1, n) * np.exp(-t / 0.006) * 0.12
    env = (1.0 - np.exp(-t / 0.005)) * np.exp(-t / (dur * 0.72))
    env *= np.clip((dur + 0.08 - t) / 0.04, 0.0, 1.0)
    return pan_stereo(sig * env * vel + click * vel, pan=-0.15)


def synth_pizzicato_chord(midi_notes, dur, vel=0.7, pan=0.25):
    n = int((dur + 0.06) * SR)
    t = np.arange(n) / SR
    mix = np.zeros(n)
    for i, m in enumerate(midi_notes):
        f = midi_to_freq(m)
        # Tiny human strum stagger (4ms per string)
        t_s = np.maximum(0.0, t - i * 0.004)
        ph = 2.0 * np.pi * f * t_s
        # Plucked string + warm body resonance
        s = (
            0.65 * np.sin(ph)
            + 0.30 * np.sin(2.0 * ph) * np.exp(-t_s / 0.09)
            + 0.16 * np.sin(3.0 * ph) * np.exp(-t_s / 0.05)
            + 0.08 * np.sin(4.0 * ph) * np.exp(-t_s / 0.03)
        )
        env = (1.0 - np.exp(-t_s / 0.003)) * np.exp(-t_s / 0.13) * (t >= i * 0.004)
        mix += s * env
    mix /= max(len(midi_notes) ** 0.6, 1.0)
    return pan_stereo(mix * vel, pan=pan)


def synth_vibraphone(midi_note, dur, vel=0.85, pan=0.0):
    n = int((dur + 0.25) * SR)
    t = np.arange(n) / SR
    f = midi_to_freq(midi_note)
    # Vibraphone motor tremolo (6.2 Hz) + warm FM mallet strike (1:4 metallic bar ratio)
    trem = 1.0 - 0.22 * (0.5 - 0.5 * np.cos(2.0 * np.pi * 6.2 * t))
    mod_env = np.exp(-t / 0.022)
    mod = 1.35 * mod_env * np.sin(2.0 * np.pi * (f * 4.0) * t)
    sig = (
        0.82 * np.sin(2.0 * np.pi * f * t + mod)
        + 0.18 * np.sin(2.0 * np.pi * (f * 2.0) * t) * np.exp(-t / 0.12)
        + 0.09 * np.sin(2.0 * np.pi * (f * 3.0) * t) * np.exp(-t / 0.04)
    )
    env = (1.0 - np.exp(-t / 0.003)) * np.exp(-t / (dur * 0.95 + 0.12))
    env *= np.clip((dur + 0.25 - t) / 0.06, 0.0, 1.0)
    return pan_stereo(sig * env * trem * vel, pan=pan)


def synth_clarinet(midi_note, dur, vel=0.75, pan=-0.25, slide_from=None):
    n = int((dur + 0.05) * SR)
    t = np.arange(n) / SR
    f_target = midi_to_freq(midi_note)
    if slide_from is not None:
        f_start = midi_to_freq(slide_from)
        u = np.clip(t / min(0.06, dur * 0.35), 0.0, 1.0)
        f_base = f_start + (f_target - f_start) * (u * u * (3.0 - 2.0 * u))
    else:
        f_base = np.full(n, f_target)
    # Expressive vibrato after 80ms
    vib = 1.0 + 0.0045 * smooth_step(0.05, 0.16, t) * np.sin(2.0 * np.pi * 5.6 * t)
    ph = 2.0 * np.pi * np.cumsum(f_base * vib) / SR
    # Clarinet is dominated by odd harmonics (1, 3, 5, 7) with warm woody formant
    sig = (
        0.68 * np.sin(ph)
        + 0.34 * np.sin(3.0 * ph)
        + 0.14 * np.sin(5.0 * ph)
        + 0.06 * np.sin(2.0 * ph)
        + 0.05 * np.sin(7.0 * ph)
    )
    attack = smooth_step(0.0, 0.022, t)
    release = smooth_step(dur + 0.05, dur - 0.015, t)
    sustain = np.exp(-t / (dur * 1.8))
    return pan_stereo(sig * attack * release * sustain * vel, pan=pan)


def smooth_step(a, b, x):
    u = np.clip((x - a) / (b - a), 0.0, 1.0)
    return u * u * (3.0 - 2.0 * u)


def synth_drum_kick(vel=0.8):
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    f = 52.0 + 95.0 * np.exp(-t / 0.022)
    ph = 2.0 * np.pi * np.cumsum(f) / SR
    sig = np.sin(ph) * np.exp(-t / 0.075)
    thump = np.sin(2.0 * np.pi * 160.0 * t) * np.exp(-t / 0.012) * 0.25
    return pan_stereo((sig + thump) * vel, pan=0.0)


def synth_brush_snare(vel=0.5, accent=False):
    dur = 0.14 if accent else 0.09
    n = int(dur * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(42 if accent else 17)
    noise = rng.normal(0, 1, n)
    # Bandpass/highpass feel via difference filter
    brushed = np.diff(noise, prepend=0.0) * 0.65 + noise * 0.35
    body = np.sin(2.0 * np.pi * 195.0 * t) * np.exp(-t / 0.025) * (0.35 if accent else 0.15)
    env = (1.0 - np.exp(-t / 0.004)) * np.exp(-t / (0.048 if accent else 0.032))
    return pan_stereo((brushed * env * 0.45 + body) * vel, pan=0.12)


def synth_hihat(open_hat=False, vel=0.35):
    dur = 0.18 if open_hat else 0.045
    n = int(dur * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(99 if open_hat else 73)
    noise = rng.normal(0, 1, n)
    hp = np.diff(np.diff(noise, prepend=0.0), prepend=0.0) * 0.4
    # Metallic shimmer partials
    metal = (
        np.sin(2.0 * np.pi * 3420.0 * t)
        + np.sin(2.0 * np.pi * 5180.0 * t)
        + np.sin(2.0 * np.pi * 7310.0 * t)
    ) * 0.08
    env = np.exp(-t / (0.075 if open_hat else 0.014))
    return pan_stereo((hp + metal) * env * vel, pan=-0.22)


def synth_woodblock(pitch_hz=820.0, vel=0.5):
    n = int(0.065 * SR)
    t = np.arange(n) / SR
    sig = (
        np.sin(2.0 * np.pi * pitch_hz * t)
        + 0.35 * np.sin(2.0 * np.pi * (pitch_hz * 2.42) * t) * np.exp(-t / 0.008)
    )
    env = (1.0 - np.exp(-t / 0.0015)) * np.exp(-t / 0.016)
    return pan_stereo(sig * env * vel, pan=0.32)


# ==============================================================================
# 1. BACKGROUND MUSIC GENERATOR (32.0s SEAMLESS SWING LOOP @ 120 BPM)
# ==============================================================================

def build_background_music():
    # 120 BPM -> 1 beat = 0.5s, 1 bar (4/4) = 2.0s, 16 bars = 32.0s
    beat_sec = 0.5
    bar_sec = 2.0
    total_bars = 16
    total_sec = total_bars * bar_sec
    buf = np.zeros((int(total_sec * SR), 2), dtype=np.float64)

    def btime(bar, beat):
        """Convert (bar 0..15, swing beat) to seconds. Swing 8th note (.5) is delayed to 0.64 of a beat."""
        whole = math.floor(beat)
        frac = beat - whole
        if abs(frac - 0.5) < 0.05:
            frac = 0.64
        return bar * bar_sec + (whole + frac) * beat_sec

    # --- 16-Bar Chord & Walking Bass Chart (in C Minor / G7 Parlour Swing) ---
    # Each bar has: (4 walking bass MIDI notes, comping chord MIDI notes)
    chart = [
        # A1 (Bars 0-3): Cm6 | Fm6 | G7 | Cm6
        ([48, 51, 55, 57], [60, 63, 67, 69]),
        ([41, 44, 48, 50], [60, 65, 68, 74]),
        ([43, 47, 50, 53], [59, 62, 65, 68]),
        ([48, 55, 51, 48], [60, 63, 67, 69]),
        # A2 (Bars 4-7): Cm6 | Ab7 | G7 | Cm6 -> C7
        ([48, 51, 55, 57], [60, 63, 67, 69]),
        ([44, 48, 51, 54], [60, 63, 66, 68]),
        ([43, 47, 50, 43], [59, 62, 65, 68]),
        ([48, 50, 51, 52], [60, 64, 67, 70]),
        # B Bridge (Bars 8-11): Fm7 | Bb7 | EbMaj7 | Ab7 -> G7
        ([41, 44, 48, 51], [60, 65, 68, 72]),
        ([46, 50, 53, 56], [62, 65, 68, 70]),
        ([39, 43, 46, 50], [63, 67, 70, 74]),
        ([44, 48, 43, 47], [59, 62, 65, 68]),
        # A3 Turnaround (Bars 12-15): Cm6 | Fm6 | G7b9 | Cm6 (G7 pickup)
        ([48, 51, 55, 57], [60, 63, 67, 69]),
        ([41, 44, 48, 50], [60, 65, 68, 74]),
        ([43, 47, 50, 53], [59, 62, 65, 68]),
        ([48, 51, 43, 47], [60, 63, 67, 69]),
    ]

    for bar_idx, (bass_notes, chord_notes) in enumerate(chart):
        # 1. Walking Upright Bass on beats 0, 1, 2, 3 + occasional swing ghost pickup
        for b_i, bn in enumerate(bass_notes):
            add_Wrapped(buf, synth_upright_bass(bn, 0.44, vel=0.52), btime(bar_idx, b_i))
        if bar_idx % 2 == 1:
            add_Wrapped(buf, synth_upright_bass(bass_notes[-1] + 1, 0.16, vel=0.30), btime(bar_idx, 3.5))

        # 2. Pizzicato Strings / Parlour Comping on Charleston & Off-beats (beats 1.0, 2.5, 3.5)
        add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.22, vel=0.36, pan=0.28), btime(bar_idx, 1.0))
        add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.20, vel=0.32, pan=-0.28), btime(bar_idx, 2.5))
        if bar_idx % 2 == 0:
            add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.18, vel=0.26, pan=0.20), btime(bar_idx, 3.5))

        # 3. Swing Brush Drums & Percussion
        for beat in (0.0, 2.0):
            add_Wrapped(buf, synth_drum_kick(vel=0.42), btime(bar_idx, beat))
        for beat in (1.0, 3.0):
            add_Wrapped(buf, synth_brush_snare(vel=0.34, accent=True), btime(bar_idx, beat))
            add_Wrapped(buf, synth_hihat(open_hat=False, vel=0.24), btime(bar_idx, beat))
        for beat in (0.0, 1.5, 2.0, 3.5):
            add_Wrapped(buf, synth_hihat(open_hat=(beat == 3.5 and bar_idx % 4 == 3), vel=0.20), btime(bar_idx, beat))
        if bar_idx in (3, 7, 11, 15):
            add_Wrapped(buf, synth_woodblock(860.0, vel=0.25), btime(bar_idx, 2.5))
            add_Wrapped(buf, synth_woodblock(680.0, vel=0.28), btime(bar_idx, 3.0))
            add_Wrapped(buf, synth_brush_snare(vel=0.30, accent=False), btime(bar_idx, 3.5))

    # --- 4. Vibraphone Lead Melody (Playful Mischievous Theme) ---
    # Each tuple: (bar, beat, midi_note, dur_sec, vel)
    vibe_melody = [
        # Bar 0 (Cm6): sneaky rising motif
        (0, 0.0, 72, 0.40, 0.55), (0, 1.0, 75, 0.35, 0.50), (0, 1.5, 74, 0.25, 0.45),
        (0, 2.0, 72, 0.35, 0.52), (0, 2.5, 69, 0.35, 0.48), (0, 3.0, 67, 0.45, 0.50),
        # Bar 1 (Fm6): answer phrase
        (1, 0.0, 68, 0.40, 0.52), (1, 1.0, 72, 0.35, 0.50), (1, 1.5, 75, 0.25, 0.48),
        (1, 2.0, 74, 0.65, 0.55), (1, 3.5, 72, 0.25, 0.42),
        # Bar 2 (G7): chromatic tiptoe
        (2, 0.0, 71, 0.35, 0.52), (2, 0.5, 72, 0.25, 0.46), (2, 1.0, 74, 0.35, 0.52),
        (2, 1.5, 75, 0.25, 0.48), (2, 2.0, 77, 0.40, 0.56), (2, 3.0, 71, 0.40, 0.50),
        # Bar 3 (Cm6): resolution + cheeky bell grace
        (3, 0.0, 72, 0.85, 0.58), (3, 2.0, 79, 0.25, 0.46), (3, 2.5, 75, 0.25, 0.44), (3, 3.0, 72, 0.40, 0.50),

        # Bar 4 (Cm6): A2 variation higher
        (4, 0.0, 79, 0.40, 0.56), (4, 1.0, 75, 0.35, 0.50), (4, 1.5, 72, 0.25, 0.46),
        (4, 2.0, 74, 0.35, 0.52), (4, 2.5, 75, 0.30, 0.50), (4, 3.0, 79, 0.45, 0.54),
        # Bar 5 (Ab7): bluesy inflection
        (5, 0.0, 78, 0.22, 0.48), (5, 0.25, 79, 0.45, 0.55), (5, 1.5, 75, 0.30, 0.48),
        (5, 2.0, 72, 0.45, 0.52), (5, 3.0, 68, 0.40, 0.48),
        # Bar 6 (G7): suspense climb
        (6, 0.0, 71, 0.35, 0.50), (6, 1.0, 74, 0.35, 0.52), (6, 2.0, 77, 0.35, 0.54),
        (6, 2.5, 80, 0.30, 0.56), (6, 3.0, 79, 0.45, 0.54),
        # Bar 7 (Cm6 -> C7): lead into bridge
        (7, 0.0, 72, 0.75, 0.56), (7, 2.0, 76, 0.35, 0.48), (7, 2.5, 79, 0.30, 0.50), (7, 3.0, 82, 0.45, 0.54),

        # Bars 8-11 (Bridge): sparkling vibraphone counter-chords while clarinet takes lead
        (8, 0.0, 80, 0.55, 0.44), (8, 2.0, 77, 0.55, 0.42),
        (9, 0.0, 77, 0.55, 0.44), (9, 2.0, 74, 0.55, 0.42),
        (10, 0.0, 79, 0.55, 0.44), (10, 2.0, 75, 0.55, 0.42),
        (11, 0.0, 75, 0.45, 0.44), (11, 1.0, 74, 0.45, 0.44), (11, 2.0, 71, 0.65, 0.48),

        # Bars 12-15 (Final A3 & Turnaround)
        (12, 0.0, 72, 0.40, 0.55), (12, 1.0, 75, 0.35, 0.50), (12, 1.5, 74, 0.25, 0.46),
        (12, 2.0, 72, 0.35, 0.52), (12, 2.5, 79, 0.35, 0.54), (12, 3.0, 75, 0.45, 0.50),
        (13, 0.0, 77, 0.40, 0.54), (13, 1.0, 75, 0.35, 0.50), (13, 1.5, 72, 0.25, 0.48),
        (13, 2.0, 68, 0.45, 0.50), (13, 3.0, 74, 0.45, 0.52),
        (14, 0.0, 75, 0.35, 0.52), (14, 0.5, 74, 0.25, 0.48), (14, 1.0, 71, 0.35, 0.50),
        (14, 1.5, 67, 0.25, 0.46), (14, 2.0, 68, 0.35, 0.50), (14, 3.0, 71, 0.40, 0.52),
        (15, 0.0, 72, 0.85, 0.58), (15, 2.0, 67, 0.30, 0.46), (15, 2.5, 71, 0.25, 0.48), (15, 3.0, 74, 0.35, 0.50),
    ]
    for bar, beat, m, dur, vel in vibe_melody:
        pan = ((m - 72) / 14.0) * 0.35
        add_Wrapped(buf, synth_vibraphone(m, dur, vel=vel, pan=pan), btime(bar, beat))

    # --- 5. Cheeky Clarinet Lead in Bridge (Bars 8-11) & Turnaround fills ---
    clarinet_phrases = [
        # Turnaround fill in Bar 3
        (3, 1.0, 63, 0.25, 0.42, 62), (3, 1.5, 67, 0.35, 0.44, None),
        # Bridge Lead (Bars 8-11)
        (8, 0.0, 65, 0.42, 0.52, 63), (8, 1.0, 68, 0.35, 0.50, None), (8, 1.5, 72, 0.30, 0.52, None),
        (8, 2.0, 75, 0.55, 0.56, 73), (8, 3.5, 72, 0.25, 0.46, None),
        (9, 0.0, 74, 0.42, 0.54, 72), (9, 1.0, 70, 0.35, 0.50, None), (9, 1.5, 68, 0.25, 0.48, None),
        (9, 2.0, 65, 0.65, 0.52, None),
        (10, 0.0, 67, 0.42, 0.52, 65), (10, 1.0, 70, 0.35, 0.50, None), (10, 1.5, 75, 0.30, 0.54, None),
        (10, 2.0, 74, 0.40, 0.52, None), (10, 3.0, 70, 0.40, 0.48, None),
        (11, 0.0, 68, 0.40, 0.52, None), (11, 1.0, 66, 0.40, 0.50, None), (11, 2.0, 67, 0.70, 0.55, 65),
        # Final bar sneaky pickup back to Bar 0
        (15, 1.5, 60, 0.22, 0.42, None), (15, 2.0, 63, 0.22, 0.44, None),
        (15, 2.5, 66, 0.22, 0.46, None), (15, 3.0, 67, 0.35, 0.50, None),
    ]
    for bar, beat, m, dur, vel, sl in clarinet_phrases:
        add_Wrapped(buf, synth_clarinet(m, dur, vel=vel, pan=-0.22, slide_from=sl), btime(bar, beat))

    buf = simple_parlour_reverb(buf, wet=0.16, wrap=True)
    save_wav("BoB_Music.wav", buf, normalize_peak=0.85)


def synth_accordion(midi_notes, dur, vel=0.70, pan=0.22):
    """Warm European parlour bandoneon / accordion with dual musette detuned reeds."""
    if isinstance(midi_notes, (int, float)):
        midi_notes = [midi_notes]
    n = int((dur + 0.05) * SR)
    t = np.arange(n) / SR
    mix = np.zeros(n)
    bellows = smooth_step(0.0, 0.025, t) * smooth_step(dur + 0.05, dur - 0.015, t) * (
        0.92 + 0.08 * np.sin(2.0 * np.pi * 5.2 * t)
    )
    for m in midi_notes:
        f = midi_to_freq(m)
        # Two musette reeds slightly detuned (+1.8 Hz / -1.5 Hz) for rich tango/cafe character
        for det in (-1.4, 1.6):
            ph = 2.0 * np.pi * (f + det) * t
            s = (
                0.55 * np.sin(ph)
                + 0.32 * np.sin(2.0 * ph)
                + 0.22 * np.sin(3.0 * ph)
                + 0.12 * np.sin(4.0 * ph)
                + 0.06 * np.sin(5.0 * ph)
            )
            mix += 0.5 * s
    mix /= max(len(midi_notes) ** 0.65, 1.0)
    return pan_stereo(mix * bellows * vel, pan=pan)


def synth_tack_piano(midi_notes, dur, vel=0.75, pan=-0.22):
    """Bright vintage speakeasy upright tack piano with honky-tonk unison string detuning."""
    if isinstance(midi_notes, (int, float)):
        midi_notes = [midi_notes]
    n = int((dur + 0.10) * SR)
    t = np.arange(n) / SR
    mix = np.zeros(n)
    for i, m in enumerate(midi_notes):
        f = midi_to_freq(m)
        t_s = np.maximum(0.0, t - i * 0.003)
        # Two slightly detuned piano strings per key + metallic hammer tack transient
        for det in (-0.8, 0.9):
            ph = 2.0 * np.pi * (f + det) * t_s
            s = (
                0.62 * np.sin(ph)
                + 0.34 * np.sin(2.0 * ph) * np.exp(-t_s / 0.22)
                + 0.20 * np.sin(3.0 * ph) * np.exp(-t_s / 0.14)
                + 0.11 * np.sin(4.0 * ph) * np.exp(-t_s / 0.08)
                + 0.06 * np.sin(6.0 * ph) * np.exp(-t_s / 0.03)
            )
            mix += 0.5 * s * (t >= i * 0.003)
    env = (1.0 - np.exp(-t / 0.0025)) * np.exp(-t / (dur * 0.80 + 0.08))
    env *= np.clip((dur + 0.10 - t) / 0.04, 0.0, 1.0)
    mix /= max(len(midi_notes) ** 0.65, 1.0)
    return pan_stereo(mix * env * vel, pan=pan)


def synth_muted_brass(midi_note, dur, vel=0.72, pan=0.18, wah_open=True):
    """Punchy cup/harmon-muted jazz trumpet for speakeasy & showdown rounds."""
    n = int((dur + 0.05) * SR)
    t = np.arange(n) / SR
    f = midi_to_freq(midi_note)
    # Slight lip scoop on attack + jazz vibrato
    scoop = 1.0 - 0.015 * np.exp(-t / 0.018) + 0.005 * smooth_step(0.06, 0.18, t) * np.sin(2.0 * np.pi * 5.8 * t)
    ph = 2.0 * np.pi * np.cumsum(f * scoop) / SR
    # Brass buzz rich in mid-harmonics (2..6) simulating a mute formant
    w = smooth_step(0.0, min(0.09, dur * 0.5), t) if wah_open else np.exp(-t / 0.12)
    sig = (
        0.30 * np.sin(ph)
        + 0.48 * np.sin(2.0 * ph)
        + (0.38 + 0.20 * w) * np.sin(3.0 * ph)
        + (0.25 + 0.22 * w) * np.sin(4.0 * ph)
        + 0.16 * np.sin(5.0 * ph)
        + 0.09 * np.sin(6.0 * ph)
    )
    env = smooth_step(0.0, 0.012, t) * smooth_step(dur + 0.05, dur - 0.012, t) * np.exp(-t / (dur * 1.4 + 0.08))
    return pan_stereo(sig * env * vel, pan=pan)


# ==============================================================================
# 1B. ROUND 2, ROUND 3, AND ROUND 4+ SOUNDTRACK GENERATORS
# ==============================================================================

def build_round2_music():
    """Round 2: 'Midnight Tango & Tricks' (126 BPM, D Harmonic Minor / A7 Habanera-Swing, 16 bars = 30.48s)."""
    bpm = 126.0
    beat_sec = 60.0 / bpm
    bar_sec = 4.0 * beat_sec
    total_bars = 16
    buf = np.zeros((int(round(total_bars * bar_sec * SR)), 2), dtype=np.float64)

    def btime(bar, beat):
        return bar * bar_sec + beat * beat_sec

    # 16-bar Habanera / Parlour Tango progression in D Harmonic Minor
    # Each bar: (Habanera bass notes on [0.0, 1.5, 2.0, 3.0], accordion/staccato chord)
    chart = [
        # A1 (Bars 0-3): Dm6 | Gm6 | A7b9 | Dm6
        ([50, 57, 50, 53], [62, 65, 69, 71]),
        ([43, 50, 43, 46], [62, 67, 70, 76]),
        ([45, 52, 55, 49], [61, 64, 67, 70]),
        ([50, 57, 53, 50], [62, 65, 69, 74]),
        # A2 (Bars 4-7): Dm6 | Bb7 | A7b9 | Dm6 -> D7
        ([50, 57, 50, 53], [62, 65, 69, 71]),
        ([46, 53, 56, 50], [62, 65, 68, 70]),
        ([45, 52, 55, 45], [61, 64, 67, 70]),
        ([50, 52, 53, 54], [62, 66, 69, 72]),
        # B Bridge (Bars 8-11): Gm7 | C7 | FMaj7 | BbMaj7 -> A7b9
        ([43, 46, 50, 53], [62, 67, 70, 74]),
        ([48, 52, 55, 58], [64, 67, 70, 72]),
        ([41, 45, 48, 52], [65, 69, 72, 76]),
        ([46, 50, 45, 49], [61, 64, 67, 70]),
        # A3 Climax (Bars 12-15): Dm6 | Gm6 | E7b5 -> A7b9 | Dm6 (turnaround)
        ([50, 57, 50, 53], [62, 65, 69, 71]),
        ([43, 50, 43, 46], [62, 67, 70, 76]),
        ([40, 44, 45, 49], [61, 64, 67, 70]),
        ([50, 53, 45, 49], [62, 65, 69, 74]),
    ]

    habanera_beats = [0.0, 1.5, 2.0, 3.0]
    habanera_durs = [0.55, 0.22, 0.38, 0.38]

    for bar_idx, (bass_notes, chord_notes) in enumerate(chart):
        # 1. Habanera / Tango Upright Bass pulse (0.0, 1.5, 2.0, 3.0)
        for b_beat, b_dur, bn in zip(habanera_beats, habanera_durs, bass_notes):
            add_Wrapped(buf, synth_upright_bass(bn, b_dur, vel=0.54), btime(bar_idx, b_beat))

        # 2. Staccato Accordion & Pizzicato Tango Stabs on beats 1.0, 1.5, 2.5, 3.0
        add_Wrapped(buf, synth_accordion(chord_notes, 0.18, vel=0.34, pan=0.26), btime(bar_idx, 1.0))
        add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.16, vel=0.34, pan=-0.26), btime(bar_idx, 1.5))
        add_Wrapped(buf, synth_accordion(chord_notes, 0.18, vel=0.32, pan=0.26), btime(bar_idx, 2.5))
        add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.22, vel=0.36, pan=-0.26), btime(bar_idx, 3.0))

        # 3. Crisp Tango Percussion (Kick on Habanera, Snare rim/brush on 1 & 3, Woodblock clave)
        for kb in (0.0, 1.5, 2.0):
            add_Wrapped(buf, synth_drum_kick(vel=0.44 if kb == 0.0 else 0.34), btime(bar_idx, kb))
        for sb in (1.0, 3.0):
            add_Wrapped(buf, synth_brush_snare(vel=0.36, accent=True), btime(bar_idx, sb))
        for hb in (0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5):
            add_Wrapped(buf, synth_hihat(open_hat=(hb == 3.5 and bar_idx % 4 == 3), vel=0.17), btime(bar_idx, hb))
        if bar_idx % 2 == 1:
            add_Wrapped(buf, synth_woodblock(920.0, vel=0.24), btime(bar_idx, 1.5))
            add_Wrapped(buf, synth_woodblock(740.0, vel=0.26), btime(bar_idx, 3.5))

    # 4. Sultry Clarinet & Vibraphone Tango Lead in D Harmonic Minor
    tango_lead = [
        # Bars 0-3: Dramatic D minor tango motif (D5 - F5 - A5 - G#5 - A5)
        (0, 0.0, 74, 0.55, 0.56, 72), (0, 1.5, 77, 0.22, 0.50, None), (0, 2.0, 81, 0.40, 0.58, 79), (0, 3.0, 80, 0.22, 0.50, None), (0, 3.5, 81, 0.22, 0.52, None),
        (1, 0.0, 79, 0.55, 0.55, None), (1, 1.5, 77, 0.22, 0.48, None), (1, 2.0, 76, 0.38, 0.52, None), (1, 3.0, 74, 0.38, 0.50, None),
        (2, 0.0, 73, 0.38, 0.54, 71), (2, 1.0, 76, 0.38, 0.52, None), (2, 2.0, 79, 0.38, 0.56, None), (2, 3.0, 77, 0.22, 0.50, None), (2, 3.5, 76, 0.22, 0.48, None),
        (3, 0.0, 74, 0.85, 0.58, None), (3, 2.5, 69, 0.20, 0.46, None), (3, 3.0, 73, 0.20, 0.48, None), (3, 3.5, 74, 0.20, 0.50, None),
        # Bars 4-7: Second phrase climbing to Bb5
        (4, 0.0, 81, 0.55, 0.56, 79), (4, 1.5, 77, 0.22, 0.50, None), (4, 2.0, 74, 0.38, 0.52, None), (4, 3.0, 81, 0.38, 0.56, None),
        (5, 0.0, 82, 0.55, 0.58, 80), (5, 1.5, 80, 0.22, 0.50, None), (5, 2.0, 77, 0.40, 0.54, None), (5, 3.0, 74, 0.38, 0.50, None),
        (6, 0.0, 76, 0.35, 0.52, None), (6, 1.0, 77, 0.35, 0.52, None), (6, 2.0, 79, 0.35, 0.55, None), (6, 3.0, 73, 0.40, 0.54, None),
        (7, 0.0, 74, 0.70, 0.58, None), (7, 2.0, 78, 0.35, 0.50, None), (7, 3.0, 81, 0.38, 0.54, None),
        # Bars 8-11: Accordion + Vibraphone lyrical bridge
        (8, 0.0, 82, 0.55, 0.54, 80), (8, 1.5, 79, 0.22, 0.48, None), (8, 2.0, 77, 0.40, 0.52, None), (8, 3.0, 74, 0.38, 0.50, None),
        (9, 0.0, 79, 0.55, 0.54, 77), (9, 1.5, 76, 0.22, 0.48, None), (9, 2.0, 72, 0.40, 0.52, None), (9, 3.0, 76, 0.38, 0.50, None),
        (10, 0.0, 77, 0.55, 0.54, 75), (10, 1.5, 74, 0.22, 0.48, None), (10, 2.0, 72, 0.40, 0.52, None), (10, 3.0, 69, 0.38, 0.50, None),
        (11, 0.0, 70, 0.40, 0.52, None), (11, 1.0, 74, 0.38, 0.52, None), (11, 2.0, 73, 0.65, 0.56, 71),
        # Bars 12-15: Finale & Turnaround
        (12, 0.0, 74, 0.55, 0.56, 72), (12, 1.5, 77, 0.22, 0.50, None), (12, 2.0, 81, 0.40, 0.58, None), (12, 3.0, 86, 0.38, 0.58, 84),
        (13, 0.0, 82, 0.40, 0.56, None), (13, 1.0, 79, 0.35, 0.52, None), (13, 2.0, 77, 0.38, 0.52, None), (13, 3.0, 74, 0.38, 0.50, None),
        (14, 0.0, 76, 0.25, 0.52, None), (14, 0.5, 77, 0.25, 0.52, None), (14, 1.0, 76, 0.35, 0.52, None), (14, 2.0, 73, 0.38, 0.54, None), (14, 3.0, 69, 0.38, 0.52, None),
        (15, 0.0, 74, 0.80, 0.58, None), (15, 2.5, 69, 0.20, 0.48, None), (15, 3.0, 71, 0.20, 0.50, None), (15, 3.5, 73, 0.20, 0.52, None),
    ]
    for bar, beat, m, dur, vel, sl in tango_lead:
        t0 = btime(bar, beat)
        add_Wrapped(buf, synth_clarinet(m, dur, vel=vel * 0.82, pan=-0.20, slide_from=sl), t0)
        add_Wrapped(buf, synth_vibraphone(m, dur * 0.9, vel=vel * 0.72, pan=0.22), t0)

    buf = simple_parlour_reverb(buf, wet=0.16, wrap=True)
    save_wav("BoB_Music_Round2.wav", buf, normalize_peak=0.85)


def build_round3_music():
    """Round 3: 'Speakeasy Double-Cross' (134 BPM, F Minor / C7b9 Detective Hustle, 16 bars = 28.66s)."""
    bpm = 134.0
    beat_sec = 60.0 / bpm
    bar_sec = 4.0 * beat_sec
    total_bars = 16
    buf = np.zeros((int(round(total_bars * bar_sec * SR)), 2), dtype=np.float64)

    def btime(bar, beat):
        whole = math.floor(beat)
        frac = beat - whole
        if abs(frac - 0.5) < 0.05:
            frac = 0.63
        return bar * bar_sec + (whole + frac) * beat_sec

    # 16-bar F Minor / C7b9 Speakeasy Hustle chart
    chart = [
        # A1 (Bars 0-3): Fm6 | Bbm6 | C7b9 | Fm6
        ([41, 44, 48, 51], [60, 65, 68, 74]),
        ([46, 49, 53, 56], [58, 61, 65, 70]),
        ([48, 52, 55, 43], [60, 64, 67, 70]),
        ([41, 48, 44, 41], [60, 65, 68, 72]),
        # A2 (Bars 4-7): Fm6 | Db7 | C7b9 | Fm6 -> F7
        ([41, 44, 48, 51], [60, 65, 68, 74]),
        ([49, 53, 56, 49], [61, 65, 68, 71]),
        ([48, 52, 55, 48], [60, 64, 67, 70]),
        ([41, 43, 44, 45], [60, 63, 65, 69]),
        # B Bridge (Bars 8-11): Bbm7 | Eb7 | AbMaj7 | Db7 -> C7b9
        ([46, 49, 53, 56], [58, 61, 65, 68]),
        ([39, 43, 46, 51], [63, 67, 70, 73]),
        ([44, 48, 51, 55], [60, 63, 68, 72]),
        ([49, 53, 48, 52], [60, 64, 67, 70]),
        # A3 (Bars 12-15): Fm6 | Bbm6 | G7b5 -> C7b9 | Fm6
        ([41, 44, 48, 51], [60, 65, 68, 74]),
        ([46, 49, 53, 56], [58, 61, 65, 70]),
        ([43, 47, 48, 52], [60, 64, 67, 70]),
        ([41, 44, 48, 43], [60, 65, 68, 72]),
    ]

    for bar_idx, (bass_notes, chord_notes) in enumerate(chart):
        # 1. Brisk Walking Upright Bass + swing ghost notes
        for b_i, bn in enumerate(bass_notes):
            add_Wrapped(buf, synth_upright_bass(bn, 0.39, vel=0.55), btime(bar_idx, b_i))
        add_Wrapped(buf, synth_upright_bass(bass_notes[1] - 1, 0.14, vel=0.28), btime(bar_idx, 1.5))
        add_Wrapped(buf, synth_upright_bass(bass_notes[3] + 1, 0.14, vel=0.30), btime(bar_idx, 3.5))

        # 2. Speakeasy Tack Piano syncopated comping + Pizzicato strings
        add_Wrapped(buf, synth_tack_piano(chord_notes, 0.20, vel=0.36, pan=-0.28), btime(bar_idx, 0.5))
        add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.18, vel=0.34, pan=0.28), btime(bar_idx, 1.5))
        add_Wrapped(buf, synth_tack_piano(chord_notes, 0.22, vel=0.38, pan=-0.25), btime(bar_idx, 2.5))
        if bar_idx % 2 == 1:
            add_Wrapped(buf, synth_tack_piano(chord_notes, 0.16, vel=0.32, pan=-0.20), btime(bar_idx, 3.5))

        # 3. Driving Speakeasy Shuffle Drums
        for beat in (0.0, 2.0):
            add_Wrapped(buf, synth_drum_kick(vel=0.45), btime(bar_idx, beat))
        for beat in (1.0, 3.0):
            add_Wrapped(buf, synth_brush_snare(vel=0.38, accent=True), btime(bar_idx, beat))
        for beat in (0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5):
            add_Wrapped(buf, synth_hihat(open_hat=(beat == 2.5 and bar_idx % 2 == 1), vel=0.21), btime(bar_idx, beat))

    # 4. Muted Jazz Trumpet & Vibraphone Call-and-Response Lead in F Minor
    speakeasy_lead = [
        # Bars 0-3: Punchy minor-second detective riff (F5 - Ab5 - G5 - F5 - E5 - F5)
        (0, 0.0, 77, 0.32, 0.56), (0, 0.5, 80, 0.22, 0.52), (0, 1.0, 79, 0.28, 0.50), (0, 1.5, 77, 0.22, 0.48), (0, 2.0, 76, 0.28, 0.50), (0, 2.5, 77, 0.42, 0.56),
        (1, 0.0, 73, 0.35, 0.54), (1, 1.0, 77, 0.32, 0.52), (1, 1.5, 80, 0.24, 0.50), (1, 2.0, 79, 0.55, 0.56),
        (2, 0.0, 76, 0.32, 0.54), (2, 0.5, 77, 0.22, 0.50), (2, 1.0, 79, 0.32, 0.54), (2, 1.5, 82, 0.24, 0.56), (2, 2.0, 80, 0.35, 0.54), (2, 3.0, 76, 0.35, 0.52),
        (3, 0.0, 77, 0.70, 0.58), (3, 2.0, 72, 0.24, 0.48), (3, 2.5, 75, 0.24, 0.50), (3, 3.0, 76, 0.32, 0.52),
        # Bars 4-7: Higher variation
        (4, 0.0, 84, 0.35, 0.58), (4, 1.0, 80, 0.30, 0.52), (4, 1.5, 77, 0.24, 0.50), (4, 2.0, 79, 0.32, 0.54), (4, 2.5, 80, 0.40, 0.56),
        (5, 0.0, 83, 0.24, 0.52), (5, 0.5, 84, 0.35, 0.58), (5, 1.5, 80, 0.28, 0.50), (5, 2.0, 77, 0.40, 0.54), (5, 3.0, 73, 0.35, 0.50),
        (6, 0.0, 76, 0.32, 0.54), (6, 1.0, 79, 0.32, 0.54), (6, 2.0, 82, 0.32, 0.56), (6, 2.5, 85, 0.28, 0.58), (6, 3.0, 84, 0.38, 0.56),
        (7, 0.0, 77, 0.65, 0.58), (7, 2.0, 79, 0.28, 0.50), (7, 2.5, 81, 0.28, 0.52), (7, 3.0, 83, 0.35, 0.54),
        # Bars 8-11: Bridge Hustle
        (8, 0.0, 85, 0.40, 0.56), (8, 1.0, 82, 0.32, 0.52), (8, 2.0, 77, 0.35, 0.52), (8, 3.0, 80, 0.35, 0.54),
        (9, 0.0, 82, 0.40, 0.56), (9, 1.0, 79, 0.32, 0.52), (9, 2.0, 75, 0.55, 0.54),
        (10, 0.0, 80, 0.40, 0.56), (10, 1.0, 75, 0.32, 0.52), (10, 2.0, 72, 0.35, 0.52), (10, 3.0, 75, 0.35, 0.52),
        (11, 0.0, 77, 0.35, 0.54), (11, 1.0, 73, 0.35, 0.52), (11, 2.0, 76, 0.60, 0.58),
        # Bars 12-15: Climax & Turnaround
        (12, 0.0, 77, 0.32, 0.56), (12, 0.5, 80, 0.22, 0.52), (12, 1.0, 84, 0.32, 0.56), (12, 2.0, 85, 0.35, 0.58), (12, 3.0, 80, 0.35, 0.54),
        (13, 0.0, 82, 0.35, 0.56), (13, 1.0, 80, 0.32, 0.52), (13, 1.5, 77, 0.24, 0.50), (13, 2.0, 73, 0.38, 0.52), (13, 3.0, 77, 0.35, 0.54),
        (14, 0.0, 79, 0.32, 0.54), (14, 0.5, 80, 0.22, 0.52), (14, 1.0, 79, 0.32, 0.54), (14, 2.0, 76, 0.35, 0.54), (14, 3.0, 72, 0.35, 0.52),
        (15, 0.0, 77, 0.70, 0.58), (15, 2.0, 72, 0.24, 0.50), (15, 2.5, 76, 0.24, 0.52), (15, 3.0, 79, 0.32, 0.54),
    ]
    for bar, beat, m, dur, vel in speakeasy_lead:
        t0 = btime(bar, beat)
        add_Wrapped(buf, synth_muted_brass(m, dur, vel=vel * 0.78, pan=0.20, wah_open=(dur > 0.3)), t0)
        add_Wrapped(buf, synth_vibraphone(m, dur, vel=vel * 0.68, pan=-0.18), t0)

    buf = simple_parlour_reverb(buf, wet=0.16, wrap=True)
    save_wav("BoB_Music_Round3.wav", buf, normalize_peak=0.85)


def build_round4_music():
    """Round 4+: 'Last Candy Standing' (142 BPM, G Harmonic Minor / D7#9 Showdown Swing, 16 bars = 27.04s)."""
    bpm = 142.0
    beat_sec = 60.0 / bpm
    bar_sec = 4.0 * beat_sec
    total_bars = 16
    buf = np.zeros((int(round(total_bars * bar_sec * SR)), 2), dtype=np.float64)

    def btime(bar, beat):
        whole = math.floor(beat)
        frac = beat - whole
        if abs(frac - 0.5) < 0.05:
            frac = 0.62
        return bar * bar_sec + (whole + frac) * beat_sec

    # 16-bar High-Stakes Showdown Gypsy-Swing in G Harmonic Minor
    chart = [
        # A1 (Bars 0-3): Gm6 | Cm6 | D7b9 | Gm6
        ([43, 46, 50, 53], [62, 67, 70, 76]),
        ([48, 51, 55, 58], [60, 63, 67, 72]),
        ([50, 54, 57, 45], [62, 66, 69, 72]),
        ([43, 50, 46, 43], [62, 67, 70, 74]),
        # A2 (Bars 4-7): Gm6 | Eb7 | D7b9 | Gm6 -> G7
        ([43, 46, 50, 53], [62, 67, 70, 76]),
        ([51, 55, 58, 51], [63, 67, 70, 73]),
        ([50, 54, 57, 50], [62, 66, 69, 72]),
        ([43, 45, 46, 47], [62, 65, 67, 71]),
        # B Bridge (Bars 8-11): Cm7 | F7 | BbMaj7 | EbMaj7 -> D7b9
        ([48, 51, 55, 58], [60, 63, 67, 70]),
        ([41, 45, 48, 53], [60, 65, 69, 72]),
        ([46, 50, 53, 57], [62, 65, 70, 74]),
        ([51, 55, 50, 54], [62, 66, 69, 72]),
        # A3 Showdown Finale (Bars 12-15): Gm6 | Cm6 | A7b5 -> D7b9 | Gm6
        ([43, 46, 50, 53], [62, 67, 70, 76]),
        ([48, 51, 55, 58], [60, 63, 67, 72]),
        ([45, 49, 50, 54], [62, 66, 69, 72]),
        ([43, 46, 50, 45], [62, 67, 70, 74]),
    ]

    for bar_idx, (bass_notes, chord_notes) in enumerate(chart):
        # 1. Driving fast walking bass + double-time pickups
        for b_i, bn in enumerate(bass_notes):
            add_Wrapped(buf, synth_upright_bass(bn, 0.36, vel=0.56), btime(bar_idx, b_i))
        for off_b, step in ((0.5, 1), (2.5, -1)):
            add_Wrapped(buf, synth_upright_bass(bass_notes[int(off_b)] + step, 0.14, vel=0.28), btime(bar_idx, off_b))

        # 2. Gypsy-Swing 'La Pompe' Pizzicato on every beat + Accordion & Tack Piano off-beats
        for b_i in range(4):
            add_Wrapped(buf, synth_pizzicato_chord(chord_notes, 0.14, vel=0.35 if b_i in (1, 3) else 0.25, pan=0.25), btime(bar_idx, b_i))
        add_Wrapped(buf, synth_accordion(chord_notes, 0.16, vel=0.32, pan=-0.26), btime(bar_idx, 1.5))
        add_Wrapped(buf, synth_tack_piano(chord_notes, 0.16, vel=0.34, pan=0.20), btime(bar_idx, 2.5))
        add_Wrapped(buf, synth_accordion(chord_notes, 0.15, vel=0.30, pan=-0.22), btime(bar_idx, 3.5))

        # 3. Energetic Showdown Drums
        for beat in (0.0, 2.0):
            add_Wrapped(buf, synth_drum_kick(vel=0.46), btime(bar_idx, beat))
        for beat in (1.0, 3.0):
            add_Wrapped(buf, synth_brush_snare(vel=0.40, accent=True), btime(bar_idx, beat))
        for beat in (0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5):
            add_Wrapped(buf, synth_hihat(open_hat=(beat == 3.5 and bar_idx % 2 == 1), vel=0.22), btime(bar_idx, beat))
        if bar_idx % 4 == 3:
            for wb_b, wf in ((2.0, 960.0), (2.5, 840.0), (3.0, 720.0), (3.5, 620.0)):
                add_Wrapped(buf, synth_woodblock(wf, vel=0.26), btime(bar_idx, wb_b))

    # 4. Virtuosic Showdown Lead (Clarinet + Vibraphone + Muted Brass trading rapid G Harmonic Minor runs)
    showdown_lead = [
        # Bars 0-3: Energetic G minor arpeggio & chromatic run
        (0, 0.0, 79, 0.28, 0.58), (0, 0.5, 82, 0.20, 0.54), (0, 1.0, 86, 0.28, 0.58), (0, 1.5, 85, 0.20, 0.52), (0, 2.0, 86, 0.28, 0.58), (0, 3.0, 79, 0.32, 0.54),
        (1, 0.0, 84, 0.30, 0.56), (1, 1.0, 82, 0.28, 0.54), (1, 1.5, 79, 0.20, 0.50), (1, 2.0, 75, 0.32, 0.54), (1, 3.0, 79, 0.32, 0.52),
        (2, 0.0, 78, 0.28, 0.56), (2, 0.5, 81, 0.20, 0.52), (2, 1.0, 83, 0.28, 0.56), (2, 1.5, 86, 0.22, 0.58), (2, 2.0, 84, 0.30, 0.56), (2, 3.0, 78, 0.32, 0.54),
        (3, 0.0, 79, 0.65, 0.60), (3, 2.0, 74, 0.22, 0.50), (3, 2.5, 78, 0.22, 0.52), (3, 3.0, 79, 0.30, 0.54),
        # Bars 4-7
        (4, 0.0, 86, 0.30, 0.58), (4, 1.0, 82, 0.28, 0.54), (4, 1.5, 79, 0.22, 0.52), (4, 2.0, 81, 0.28, 0.54), (4, 2.5, 82, 0.35, 0.56),
        (5, 0.0, 87, 0.32, 0.60), (5, 1.0, 85, 0.28, 0.56), (5, 1.5, 82, 0.22, 0.52), (5, 2.0, 79, 0.35, 0.54), (5, 3.0, 75, 0.32, 0.52),
        (6, 0.0, 78, 0.28, 0.56), (6, 1.0, 81, 0.28, 0.56), (6, 1.5, 84, 0.22, 0.56), (6, 2.0, 87, 0.32, 0.60), (6, 3.0, 86, 0.32, 0.58),
        (7, 0.0, 79, 0.60, 0.60), (7, 2.0, 83, 0.28, 0.54), (7, 2.5, 86, 0.24, 0.56), (7, 3.0, 89, 0.32, 0.58),
        # Bars 8-11: Bridge
        (8, 0.0, 87, 0.35, 0.58), (8, 1.0, 84, 0.28, 0.54), (8, 2.0, 79, 0.32, 0.54), (8, 3.0, 82, 0.32, 0.54),
        (9, 0.0, 84, 0.35, 0.58), (9, 1.0, 81, 0.28, 0.54), (9, 2.0, 77, 0.50, 0.56),
        (10, 0.0, 82, 0.35, 0.58), (10, 1.0, 77, 0.28, 0.54), (10, 2.0, 74, 0.32, 0.54), (10, 3.0, 77, 0.32, 0.54),
        (11, 0.0, 79, 0.32, 0.56), (11, 1.0, 75, 0.32, 0.54), (11, 2.0, 78, 0.55, 0.58),
        # Bars 12-15: Grand Showdown Turnaround
        (12, 0.0, 79, 0.28, 0.58), (12, 0.5, 82, 0.20, 0.54), (12, 1.0, 86, 0.28, 0.58), (12, 2.0, 87, 0.32, 0.60), (12, 3.0, 86, 0.32, 0.58),
        (13, 0.0, 84, 0.30, 0.58), (13, 1.0, 82, 0.28, 0.54), (13, 1.5, 79, 0.22, 0.52), (13, 2.0, 75, 0.32, 0.54), (13, 3.0, 79, 0.32, 0.54),
        (14, 0.0, 81, 0.28, 0.56), (14, 0.5, 82, 0.20, 0.54), (14, 1.0, 81, 0.28, 0.56), (14, 2.0, 78, 0.32, 0.56), (14, 3.0, 74, 0.32, 0.54),
        (15, 0.0, 79, 0.65, 0.60), (15, 2.0, 74, 0.22, 0.52), (15, 2.5, 78, 0.22, 0.54), (15, 3.0, 81, 0.30, 0.56),
    ]
    for bar, beat, m, dur, vel in showdown_lead:
        t0 = btime(bar, beat)
        add_Wrapped(buf, synth_clarinet(m, dur, vel=vel * 0.72, pan=-0.22), t0)
        add_Wrapped(buf, synth_vibraphone(m, dur, vel=vel * 0.65, pan=0.18), t0)
        if bar >= 8 or beat in (0.0, 2.0):
            add_Wrapped(buf, synth_muted_brass(m - 12, dur, vel=vel * 0.55, pan=0.12), t0)

    buf = simple_parlour_reverb(buf, wet=0.16, wrap=True)
    save_wav("BoB_Music_Round4.wav", buf, normalize_peak=0.85)


# ==============================================================================
# 2. GAMEPLAY SOUND EFFECTS (10 CLIPS)
# ==============================================================================

def svf_filter(x, fc_arr, q=1.4, mode="bp"):
    """Time-varying 2nd-order Cytomic State Variable Filter (zero metallic sine ringing)."""
    n = len(x)
    out = np.zeros(n, dtype=np.float64)
    ic1eq = 0.0
    ic2eq = 0.0
    k = 1.0 / max(q, 0.5)
    if np.isscalar(fc_arr):
        fc_arr = np.full(n, float(fc_arr))
    g_arr = np.tan(np.pi * np.clip(fc_arr, 30.0, SR * 0.45) / SR)
    a1_arr = 1.0 / (1.0 + g_arr * (g_arr + k))
    a2_arr = g_arr * a1_arr
    a3_arr = g_arr * a2_arr
    for i in range(n):
        v0 = x[i]
        a1 = a1_arr[i]
        a2 = a2_arr[i]
        a3 = a3_arr[i]
        v3 = v0 - ic2eq
        v1 = a1 * ic1eq + a2 * v3
        v2 = ic2eq + a2 * ic1eq + a3 * v3
        ic1eq = 2.0 * v1 - ic1eq
        ic2eq = 2.0 * v2 - ic2eq
        if mode == "lp":
            out[i] = v2
        elif mode == "bp":
            out[i] = v1
        else:
            out[i] = v0 - k * v1 - v2
    return out


def pink_noise(n, rng):
    """Generate warm 1/f pink noise (no harsh white-noise hiss)."""
    white = rng.normal(0.0, 1.0, n)
    freqs = np.fft.rfftfreq(n, d=1.0 / SR)
    spec = np.fft.rfft(white)
    scale = 1.0 / np.maximum(freqs, 20.0) ** 0.55
    pink = np.fft.irfft(spec * scale, n=n)
    peak = np.max(np.abs(pink))
    return pink / peak if peak > 1e-6 else pink


def synth_bite_and_chews(is_good=True):
    """Natural, zero-hiss cartoon/game candy bite + 2 'nyam-nyam' chews + swallow gulp.
    Uses discrete 1.5ms-3ms solid fracture snaps (no continuous static noise!) + warm mouth vowel formants.
    """
    dur = 0.54
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n, dtype=np.float64)
    rng = np.random.default_rng(101 if is_good else 202)

    def add_micro_snap(t0, f_start, f_end, decay_ms, amp):
        """Single discrete 1-3ms solid crack/pop impulse (zero noise hiss)."""
        i0 = int(t0 * SR)
        m_len = int(0.018 * SR)
        if i0 < 0 or i0 >= n:
            return
        k = min(m_len, n - i0)
        lt = np.arange(k) / SR
        tau = decay_ms * 0.001
        f_inst = f_end + (f_start - f_end) * np.exp(-lt / (tau * 0.7))
        ph = 2.0 * np.pi * np.cumsum(f_inst) / SR
        # Sharp wooden/crystalline impulse with 2nd harmonic
        imp = (np.sin(ph) + 0.45 * np.sin(2.0 * ph + 0.4)) * (1.0 - np.exp(-lt / 0.00025)) * np.exp(-lt / tau)
        sig[i0:i0 + k] += imp * amp

    def add_mouth_munch(t0, dur_m, pitch_hz, amp):
        """Warm closed-to-open-to-closed mouth cavity 'om/nyam' resonance."""
        i0 = int(t0 * SR)
        k = min(int(dur_m * SR), n - i0)
        if k <= 0:
            return
        lt = np.arange(k) / SR
        u = lt / dur_m
        # Jaw opens then closes: vowel formant F1 sweeps 260 Hz (closed 'm') -> 640 Hz (open 'ah') -> 250 Hz (closed 'm')
        open_curve = np.sin(np.pi * u) ** 1.3
        f0 = pitch_hz * (0.94 + 0.14 * open_curve - 0.06 * u)
        ph0 = 2.0 * np.pi * np.cumsum(f0) / SR
        # Soft glottal/cheek excitation rich in warm low harmonics
        exc = np.zeros(k)
        for h in range(1, 9):
            exc += (1.0 / (h ** 1.15)) * np.sin(h * ph0)
        f1 = 260.0 + 390.0 * open_curve
        f2 = 720.0 + 520.0 * open_curve
        formant1 = svf_filter(exc, f1, q=3.2, mode="bp")
        formant2 = svf_filter(exc, f2, q=3.8, mode="bp")
        env = np.sin(np.pi * u) ** 0.85 * np.exp(-u * 0.8)
        sig[i0:i0 + k] += (0.72 * formant1 + 0.28 * formant2) * env * amp * 0.18

    # --- 1. INITIAL BITE ("CHOMP / KREK!" at t = 0.01s .. 0.11s) ---
    # Deep jaw closure thump + cluster of discrete hard-candy shell snaps
    add_micro_snap(0.010, 320.0, 95.0, 14.0, 0.55)
    bite_snaps = [
        (0.012, 2200.0, 620.0, 2.2, 0.65),
        (0.019, 1650.0, 480.0, 2.8, 0.55),
        (0.025, 2650.0, 820.0, 1.8, 0.50),
        (0.034, 1380.0, 410.0, 3.2, 0.60),
        (0.044, 1950.0, 580.0, 2.1, 0.42),
        (0.056, 1180.0, 360.0, 3.5, 0.38),
    ]
    for t_s, fs, fe, dec, a in bite_snaps:
        add_micro_snap(t_s + rng.uniform(-0.001, 0.001), fs, fe, dec, a)
    add_mouth_munch(0.015, 0.11, 132.0, 0.85)

    # --- 2. CHEW 1 ("NYAM" at t = 0.16s .. 0.29s) ---
    add_mouth_munch(0.160, 0.125, 138.0, 0.95)
    # Soft lip/tongue smack + 3 tiny muffled inner-mouth sugar ticks
    add_micro_snap(0.172, 480.0, 1150.0, 1.9, 0.28)
    add_micro_snap(0.195, 1250.0, 440.0, 2.4, 0.32)
    add_micro_snap(0.212, 960.0, 350.0, 2.8, 0.26)
    add_micro_snap(0.235, 1420.0, 510.0, 2.0, 0.22)

    # --- 3. CHEW 2 ("NYAM" at t = 0.31s .. 0.44s) ---
    add_mouth_munch(0.310, 0.125, 146.0 if is_good else 128.0, 0.95)
    add_micro_snap(0.322, 520.0, 1220.0, 1.8, 0.26)
    add_micro_snap(0.345, 1180.0, 420.0, 2.5, 0.28)
    add_micro_snap(0.368, 890.0, 340.0, 2.8, 0.24)

    # --- 4. CUTE SWALLOW ("GULP" at t = 0.45s .. 0.51s) ---
    add_micro_snap(0.455, 210.0, 540.0, 16.0, 0.36)
    add_micro_snap(0.468, 380.0, 780.0, 9.0, 0.25)

    peak = np.max(np.abs(sig))
    return (sig / peak if peak > 1e-6 else sig) * 0.62


def build_sfx_pickup():
    dur = 0.24
    buf = np.zeros((int(dur * SR), 2))
    # Ceramic clink + soft pop + upward wooden marimba grace (E5 -> A5)
    t = np.arange(int(0.08 * SR)) / SR
    clink = (np.sin(2 * np.pi * 2480 * t) + 0.5 * np.sin(2 * np.pi * 3890 * t)) * np.exp(-t / 0.012) * 0.35
    add_stereo(buf, pan_stereo(clink, 0.0), 0.0)
    add_stereo(buf, synth_vibraphone(76, 0.10, vel=0.75, pan=-0.15), 0.01)
    add_stereo(buf, synth_vibraphone(81, 0.14, vel=0.90, pan=0.15), 0.065)
    save_wav("BoB_Pickup.wav", simple_parlour_reverb(buf, wet=0.12))


def build_sfx_give():
    dur = 0.38
    n = int(dur * SR)
    t = np.arange(n) / SR
    buf = np.zeros((n, 2))
    # Velvet table slide whoosh + playful 3-note offer chime (A4 -> C5 -> E5)
    rng = np.random.default_rng(101)
    noise = np.convolve(rng.normal(0, 1, n), np.ones(18) / 18.0, mode="same")
    whoosh_env = np.sin(np.pi * np.clip(t / 0.24, 0.0, 1.0)) ** 2
    add_stereo(buf, pan_stereo(noise * whoosh_env * 0.25, 0.0), 0.0)
    add_stereo(buf, synth_vibraphone(69, 0.12, vel=0.65, pan=-0.25), 0.02)
    add_stereo(buf, synth_vibraphone(72, 0.12, vel=0.75, pan=0.0), 0.09)
    add_stereo(buf, synth_vibraphone(76, 0.18, vel=0.88, pan=0.25), 0.16)
    save_wav("BoB_Give.wav", simple_parlour_reverb(buf, wet=0.14))


def load_real_bite_chew_foley():
    """Load the studio-recorded candy bite + 3 chews foley WAV from Tools/Audio/candy_bite_chew_foley.wav."""
    foley_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "candy_bite_chew_foley.wav")
    if os.path.exists(foley_path):
        with wave.open(foley_path, "rb") as wf:
            raw = np.frombuffer(wf.readframes(wf.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0
            return raw.reshape((-1, wf.getnchannels()))
    return pan_stereo(synth_bite_and_chews(True), 0.0)


def build_sfx_eat_good():
    dur = 1.32
    buf = np.zeros((int(dur * SR), 2))
    # Sparkly "Yummy!" celesta/vibraphone arpeggio (C5 - E5 - G5 - B5 - C6) with parlour reverb
    notes = [(0.76, 72, 0.22, -0.25), (0.82, 76, 0.22, -0.10), (0.88, 79, 0.24, 0.05), (0.94, 83, 0.26, 0.20), (1.00, 84, 0.28, 0.0)]
    for t0, m, d, pan in notes:
        add_stereo(buf, synth_vibraphone(m, d, vel=0.68, pan=pan), t0)
    buf = simple_parlour_reverb(buf, wet=0.16)
    # Add 100% dry studio-recorded crisp candy bite + 3 natural mouth chews (0.00s .. 0.84s)
    foley = load_real_bite_chew_foley()
    add_stereo(buf, foley * 0.95, 0.00)
    save_wav("BoB_EatGood.wav", buf)


def build_sfx_eat_bad():
    dur = 1.38
    buf = np.zeros((int(dur * SR), 2))
    # Queasy descending chromatic "wah-wah" poison horn/clarinet + low tritone thud with parlour reverb
    t_wah = np.arange(int(0.58 * SR)) / SR
    f_wah = (311.13 - 135.0 * (t_wah / 0.58)) * (1.0 + 0.035 * np.sin(2 * np.pi * 11.0 * t_wah))
    ph = 2.0 * np.pi * np.cumsum(f_wah) / SR
    wah = (0.55 * np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.25 * np.sin(3 * ph) + 0.15 * np.sin(4 * ph))
    env_wah = smooth_step(0.0, 0.03, t_wah) * np.exp(-t_wah / 0.38) * (0.7 + 0.3 * np.sin(2 * np.pi * 6.5 * t_wah))
    add_stereo(buf, pan_stereo(wah * env_wah * 0.62, pan=0.0), 0.76)
    add_stereo(buf, synth_upright_bass(42, 0.45, vel=0.68), 0.78)  # F#2 tritone
    add_stereo(buf, synth_upright_bass(36, 0.45, vel=0.72), 0.92)  # C2 low drop
    buf = simple_parlour_reverb(buf, wet=0.16)
    # Add 100% dry studio-recorded crisp candy bite + 3 natural mouth chews (0.00s .. 0.84s)
    foley = load_real_bite_chew_foley()
    add_stereo(buf, foley * 0.95, 0.00)
    save_wav("BoB_EatBad.wav", buf)


def build_sfx_use_item():
    dur = 0.48
    buf = np.zeros((int(dur * SR), 2))
    # Crisp mechanical card/item snap + ascending arcane chord shimmer
    add_stereo(buf, synth_woodblock(1120.0, vel=0.65), 0.0)
    for t0, m, pan in [(0.03, 67, -0.3), (0.08, 72, -0.1), (0.13, 74, 0.1), (0.18, 79, 0.3), (0.23, 84, 0.0)]:
        add_stereo(buf, synth_vibraphone(m, 0.20, vel=0.78, pan=pan), t0)
    save_wav("BoB_UseItem.wav", simple_parlour_reverb(buf, wet=0.20))


def build_sfx_damage():
    dur = 0.42
    n = int(dur * SR)
    t = np.arange(n) / SR
    buf = np.zeros((n, 2))
    # Sub-bass heart punch + woody snap + dissonant minor cluster
    f_sub = 135.0 * np.exp(-t / 0.065) + 38.0
    sub = np.sin(2.0 * np.pi * np.cumsum(f_sub) / SR) * np.exp(-t / 0.14)
    add_stereo(buf, pan_stereo(sub * 0.95, 0.0), 0.0)
    add_stereo(buf, synth_woodblock(540.0, vel=0.75), 0.0)
    add_stereo(buf, synth_pizzicato_chord([48, 51, 54, 59], 0.28, vel=0.85, pan=0.0), 0.02)
    save_wav("BoB_Damage.wav", simple_parlour_reverb(buf, wet=0.15))


def build_sfx_elimination():
    dur = 1.35
    n = int(dur * SR)
    t = np.arange(n) / SR
    buf = np.zeros((n, 2))
    # Dramatic parlour elimination stinger: timpani boom + dark minor-diminished brass/clarinet chord + descending rattle
    add_stereo(buf, synth_drum_kick(vel=1.0), 0.0)
    add_stereo(buf, synth_brush_snare(vel=0.8, accent=True), 0.0)
    for m, pan in [(36, 0.0), (48, -0.2), (51, 0.2), (54, -0.3), (57, 0.3)]:
        add_stereo(buf, synth_clarinet(m, 0.85, vel=0.55, pan=pan), 0.02)
    for idx, m in enumerate([72, 69, 66, 63, 60, 54, 48]):
        add_stereo(buf, synth_vibraphone(m, 0.25, vel=0.65, pan=((idx - 3) * 0.15)), 0.12 + idx * 0.09)
    save_wav("BoB_Elimination.wav", simple_parlour_reverb(buf, wet=0.24))


def build_sfx_win():
    dur = 1.75
    buf = np.zeros((int(dur * SR), 2))
    # Upbeat Swing Victory Fanfare: snare pickup -> C6/9 big-band chord fanfare
    add_stereo(buf, synth_brush_snare(vel=0.55, accent=False), 0.00)
    add_stereo(buf, synth_brush_snare(vel=0.65, accent=False), 0.07)
    add_stereo(buf, synth_brush_snare(vel=0.80, accent=True), 0.14)
    fanfare = [
        (0.20, [60, 64, 67, 72], 0.18),
        (0.38, [64, 67, 72, 76], 0.18),
        (0.56, [67, 72, 76, 79], 0.20),
        (0.76, [69, 72, 76, 81], 0.18),
        (0.94, [60, 64, 67, 69, 74, 79, 84], 0.72),
    ]
    for t0, notes, d in fanfare:
        add_stereo(buf, synth_drum_kick(vel=0.75), t0)
        add_stereo(buf, synth_pizzicato_chord(notes, d, vel=0.70, pan=-0.2), t0)
        for m in notes[-3:]:
            add_stereo(buf, synth_vibraphone(m, d, vel=0.65, pan=0.2), t0)
            add_stereo(buf, synth_clarinet(m - 12, d, vel=0.42, pan=-0.15), t0)
    add_stereo(buf, synth_hihat(open_hat=True, vel=0.55), 0.94)
    save_wav("BoB_Win.wav", simple_parlour_reverb(buf, wet=0.22))


def build_sfx_waiter():
    dur = 0.78
    n = int(dur * SR)
    t = np.arange(n) / SR
    buf = np.zeros((n, 2))
    # Authentic silver service bell "DING!" (inharmonic partials) + cloche dome lift air whoosh
    bell_partials = [(1046.5, 1.0, 0.55), (2093.0, 0.65, 0.38), (3188.0, 0.42, 0.22), (4710.0, 0.28, 0.11), (6320.0, 0.15, 0.05)]
    bell = np.zeros(n)
    for freq, amp, decay in bell_partials:
        bell += amp * np.sin(2.0 * np.pi * freq * t) * np.exp(-t / decay)
    bell *= 1.0 - np.exp(-t / 0.0015)
    add_stereo(buf, pan_stereo(bell * 0.75, pan=0.08), 0.0)
    # Cloche lift whoosh starting at 0.12s
    rng = np.random.default_rng(505)
    whoosh = np.convolve(rng.normal(0, 1, n), np.ones(24) / 24.0, mode="same")
    w_env = np.sin(np.pi * np.clip((t - 0.10) / 0.42, 0.0, 1.0)) ** 2
    add_stereo(buf, pan_stereo(whoosh * w_env * 0.28, pan=-0.08), 0.0)
    add_stereo(buf, synth_vibraphone(79, 0.30, vel=0.55, pan=-0.15), 0.18)
    add_stereo(buf, synth_vibraphone(84, 0.35, vel=0.65, pan=0.15), 0.26)
    save_wav("BoB_Waiter.wav", simple_parlour_reverb(buf, wet=0.20))


def build_sfx_menu_select():
    dur = 0.12
    buf = np.zeros((int(dur * SR), 2))
    # Crisp, pleasant wooden mallet UI tap (A5 -> E6)
    add_stereo(buf, synth_woodblock(880.0, vel=0.70), 0.00)
    add_stereo(buf, synth_vibraphone(81, 0.07, vel=0.65, pan=-0.1), 0.00)
    add_stereo(buf, synth_vibraphone(88, 0.08, vel=0.75, pan=0.1), 0.028)
    save_wav("BoB_MenuSelect.wav", simple_parlour_reverb(buf, wet=0.10))


def main():
    build_background_music()
    build_round2_music()
    build_round3_music()
    build_round4_music()
    build_sfx_pickup()
    build_sfx_give()
    build_sfx_eat_good()
    build_sfx_eat_bad()
    build_sfx_use_item()
    build_sfx_damage()
    build_sfx_elimination()
    build_sfx_win()
    build_sfx_waiter()
    build_sfx_menu_select()


if __name__ == "__main__":
    main()
