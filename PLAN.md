# Aster Phase 4 — Linux Parity PLAN

- **Status:** Design decomposition approved; spec 4A approved. Implementation plan 4A is written in two parts and under final review. Phase 4 implementation starts only after v0.11.0 is published and PR #1 is merged.
- **Branch:** `phase-4-linux-parity` (proposed; branch from `main` after the release merge)
- **Baseline:** WPF Aster v0.10.0, commit `fcc6cb5`, MIT
- **Target:** Full design and behavior parity with the WPF app on Linux X11/XWayland first; Windows parity later; native Wayland deferred.
- **Stack:** Avalonia 12.1.6, .NET 10, NativeAOT. The WPF app remains the visual and behavioral oracle and is not modified.

> Phase 3 is the release-engineering phase for the L2 portable preview, not feature parity. Phase 4 closes the deferred WPF parity gap (`D-06`) on Linux. Phase 5, Windows parity, is not part of this plan. macOS remains deferred.

---

## 1. Ringkasan eksekusi

Phase 4 tidak dibuat sebagai satu spec raksasa. Pekerjaan dipecah menjadi tujuh sub-project yang masing-masing punya spec, plan, implementasi, dan acceptance gate sendiri.

1. **4A — Foundation & Shell:** effect layer, design system, settings schema, composition, shell compact/expanded Music, settings shell, tray.
2. **4B — Music + Lyrics:** media model, waveform, vinyl, accent, media recovery, lyrics provider/cache/pane.
3. **4C — Focus + Water:** timer, ring, chime, water schedule, toast, header, settings pages.
4. **4D — Shelf:** model, view, XDND drag-in/out, badges, limits, drop overlay.
5. **4E — Overlays + Mascot:** libpulse volume/mute, sysfs brightness, charging toast, mascot idle/blink/doze.
6. **4F — Camera:** V4L2 preview/still, mirror, switch, error states, lifecycle.
7. **4G — Verification & Release:** visual matrix, behavior matrix, AOT probe, soak, accessibility, performance, packaging, release candidate.

4A adalah satu-satunya prerequisite keras. Setelah 4A stabil, 4B–4F boleh berjalan paralel. 4G selalu terakhir.

**Definition of done Phase 4:**

- Semua gate 4A–4G PASS, atau PARTIAL dengan alasan dan dampak klaim yang eksplisit.
- Parity matrix WPF → Avalonia lengkap untuk seluruh fitur yang diklaim.
- Tidak ada gate P0/P1 yang masih terbuka.
- Visual review side-by-side pada 100/125/150% dan light/dark wallpaper.
- NativeAOT publish tanpa `IL2xxx`/`IL3xxx`.
- Soak 60 menit, accessibility walkthrough, dan performance budget tercatat.
- Paket Linux dan dokumentasi rilis siap; tidak ada klaim “1:1” sebelum matrix selesai.
- Windows parity tidak diklaim di Phase 4.

---

## 2. Status fase sebelumnya

| Phase | Status | Catatan |
|---|---|---|
| 0 — Foundation & Windows proof | PASS | Avalonia HUD, AOT, SMTC, WPF baseline tercatat |
| 1 — Avalonia Windows L2 | FAIL/waived | Windows L2 belum lengkap; waiver terbuka |
| 2A — Windows hardening | NOT RUN | Tidak ada host Windows pada sesi tersebut |
| 2B — Linux L2 | PARTIAL | Core PASS; click-through fractional, a11y, soak terbuka |
| 3 — Release engineering | RELEASE CANDIDATE | v0.11.0 portable preview; publish manual menunggu merge |
| 4 — Linux parity | DESIGN | Dokumen ini |

Detail historis tetap ada di `docs/specs/`, `docs/plans/`, dan `docs/results/` (local-only), serta di riwayat Git. Dokumen ini menggantikan isi `PLAN.md` sebelumnya, bukan menghapus bukti fase lama.

---

## 3. Keputusan produk dan teknis

### 3.1 Scope

- **Linux-first.** Paritas penuh dikerjakan di Linux X11/XWayland lebih dulu. Windows menyusul di fase terpisah.
- **Seluruh fitur WPF.** Music, lyrics, Camera, Shelf, Focus, Water, volume/brightness overlay, charging toast, mascot, settings 6 halaman, dan tray 8 item.
- **X11/XWayland first.** Native Wayland belum masuk Phase 4. Klik-through, no-activate, dan fullscreen detection untuk klien Wayland native tetap limitasi yang didokumentasikan.
- **Design parity, bukan pixel parity.** Layout, spacing, warna, tipografi, ikon, motion, dan behavior harus sama. Perbedaan rendering Skia vs DirectWrite, efek WPF vs Avalonia, dan bentuk ikon dicatat sebagai deviasi yang disepakati.
- **WPF adalah oracle.** Capture WPF asli disediakan owner saat boot ke Windows; source WPF dipakai sebagai referensi perilaku.

### 3.2 Window dan effect layer

- Window interaktif tetap **pill-sized**: 300×38 DIP collapsed, 500×252 DIP expanded, 160×12 DIP minimal notch. Ini jalur click-through yang sudah terbukti di KWin XWayland.
- Shadow dan ambient glow dilukis di **effect window terpisah** berukuran 760×460 DIP, transparan, topmost, tanpa fokus, dan tidak menerima input. Top glow, specular, noise, dan rim tetap di dalam island seperti WPF.
- Kandidat implementasi:
  - **A1:** Avalonia transparent window + X11 input shape.
  - **A2:** raw X11 override-redirect window + Skia render.
- Spike memilih A1/A2. Kalau keduanya gagal, fallback ke **inset effects**: satu window, glow/shadow di dalam pill, deviasi tepi panel dicatat.
- UI memakai abstraksi `IEffectLayer`, sehingga fallback tidak mengubah shell atau fitur.

### 3.3 Platform backends

| Area | Keputusan Linux | Alternatif / catatan |
|---|---|---|
| Media | MPRIS via D-Bus (sudah ada) | Perlu `TrackId`, `CanSeek`, `PropertiesChanged`, dan position resync |
| Camera | V4L2 langsung (P/Invoke ioctl + mmap) | MJPEG buffer adalah JPEG utuh; preview didecode via Skia; still full-res dengan format switch; multi-camera/hotplug belum teruji |
| Volume/mute | libpulse P/Invoke (`pa_threaded_mainloop`, subscribe) | Fallback `pactl subscribe`; event lokal terukur <10 ms |
| Brightness | Baca `/sys/class/backlight/*/actual_brightness`, poll 1 s | Tulis via logind `SetBrightness` hanya jika nanti ada kontrol; DDC/CI eksternal opsional |
| Shelf drag-out | XDND bawaan Avalonia 12.1 (`DragDrop.DoDragDropAsync`, `DataFormat.File`) | Uji ke Dolphin/Kate; bridge KWin XDND ↔ Wayland tersedia |
| Tray | StatusNotifierItem (sudah ada) | Menu diperluas menjadi 8 item; perilaku double-click berbeda per host |
| Fullscreen | X11 `_NET_WM_STATE_FULLSCREEN` watcher (sudah ada) | Klien Wayland native tidak terlihat; KWin script opsional, bukan syarat Phase 4 |
| Battery/charging | sysfs/UPower (sudah ada) | Poll 4 s boleh dipertahankan; event UPower sebagai peningkatan |
| Focus chime | `paplay`/`canberra-gtk-play` dengan WAV bundled | Fallback senyap jika backend tidak ada |
| Drag-in | Avalonia `DragDrop.AllowDrop` + `DataFormats.Files` | Drop overlay dan auto-expand wajib |

### 3.4 Settings dan AOT

- `AppSettings` naik ke **22 field + `SchemaVersion=2`**, mengikuti defaults dan clamp WPF.
- Migrasi schema v1 mempertahankan 5 field lama. Import `%LOCALAPPDATA%\DynamicIsland\settings.json` bersifat opsional karena Phase 4 Linux-first.
- JSON tetap source-generated; DTO lyrics memakai `JsonSerializerContext`.
- Regex LRC memakai `[GeneratedRegex]`, bukan `RegexOptions.Compiled`.
- Callback native (libpulse, V4L2) memakai `[UnmanagedCallersOnly]` dan di-root dengan benar.
- Camera memakai `WriteableBitmap`, bukan `System.Drawing`.
- Library baru diberi anotasi `IsAotCompatible`/trim analyzer; publish tetap menolak `IL2xxx`/`IL3xxx`.

### 3.5 Rust

Tidak ada pekerjaan Rust di Phase 4. Backend tetap C#. Rust hanya dipertimbangkan jika ada hotspot terukur yang membuat target performa tidak tercapai, dan belum ada bukti seperti itu.

---

## 4. Prinsip kerja

- **Satu spec per sub-project.** Tidak ada plan raksasa yang mencampur spike, shell, dan fitur.
- **Shared contract dibekukan setelah 4A.** Design token, settings schema, `IPlatformFactory`, `AppServices`, dan shell extension point dibuat di 4A; sub-project berikutnya mengonsumsi, bukan mendefinisikan ulang.
- **Shell dulu, fitur kemudian.** 4A menghasilkan shell yang bisa dinilai; tab non-Music boleh kosong/disabled sampai sub-project-nya.
- **Bukti, bukan asumsi.** Setiap PASS punya capture, log, CSV, atau perintah yang bisa diulang. Status hanya `PASS`, `PARTIAL`, `NOT RUN`, `BLOCKED`.
- **Performance target bersifat soft.** Sedikit di atas target tidak membatalkan, selama produk nyaman dan tidak ada regresi besar.
- **WPF tidak disentuh.** `src/DynamicIsland`, `DynamicIsland.sln`, dan `build.ps1` tetap utuh.
- **Docs local-only.** Hanya `PLAN.md`, README, CHANGELOG, dan dua screenshot yang di-track; spec/plan/result/ADR tetap di `docs/` yang di-ignore.
- **Tidak ada klaim parity sebelum 4G.** “1:1” hanya boleh dipakai setelah parity matrix dan visual review selesai.

---

### 4.1 Worktree dan commit policy

- Branch `phase-4-linux-parity` dibuat dari `main` setelah PR #1 merge. Worktree terpisah (mis. `../aster-phase4`) opsional; jangan mengubah metadata worktree dari Windows.
- Kode produk di `src/Aster.Avalonia/`; script verifikasi di `scripts/`; workflow di `.github/workflows/`; packaging di `packaging/`.
- Root docs yang boleh di-commit: `PLAN.md`, `README.md`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.txt`. `docs/*` local-only, kecuali dua screenshot yang sudah di-track.
- Jangan commit `src/DynamicIsland`, `DynamicIsland.sln`, `build.ps1`, `.gitignore`, `artifacts/`, `bin/`, `obj/`, atau file generated.
- Setiap sub-project commit hanya path yang relevan; cek `git status` sebelum commit dan jangan stage `docs/` sebagai direktori.

---

## 5. Sub-project dan dependency

| # | Sub-project | Isi | Depends | Exit gate | Size / risiko |
|---|---|---|---|---|---|
| **4A** | Foundation & Shell | Effect layer spike, design token, Inter + icon font, AppServices, settings v2, shell compact/expanded Music, settings shell, tray | — | Capture 125%; perilaku L2; AOT; keputusan effect layer | L / High |
| **4B** | Music + Lyrics | Media model, waveform, vinyl, accent, footer, media recovery, lyrics provider/cache/pane, settings Lyrics | 4A | Semua state Music + lyrics + seek; capture; AOT | M / Med |
| **4C** | Focus + Water | Focus service/ring/chime/pill, water schedule/toast/header, settings Focus/Water | 4A | Semua state focus + water rollover; capture | S / Med |
| **4D** | Shelf | Model/view, XDND drag-in/out, 8 item, badge, missing file, blue dot, drop overlay | 4A | Drag ke Dolphin/Kate; 8 item; session-only; capture | M / Med |
| **4E** | Overlays + Mascot | libpulse volume/mute, sysfs brightness, charging toast, mascot idle/blink/doze, settings Appearance | 4A | Event volume <100 ms; poll brightness; toast; mascot; fallback | M / Med |
| **4F** | Camera | V4L2 preview/still, mirror, switch, error state, lifecycle, settings mirror | 4A | 3 JPEG valid; preview/still; no-camera fallback; capture; AOT | L / High |
| **4G** | Verification & Release | Visual matrix, behavior matrix, AOT fungsional, soak, a11y, performa, packaging, docs | 4B–4F | Semua gate; tidak ada klaim parity palsu; release candidate | M / Med |

### 5.1 Dependency graph

```text
4A ──┬── 4B ──┐
     ├── 4C ──┤
     ├── 4D ──┼── 4G
     ├── 4E ──┤
     └── 4F ──┘
```

### 5.2 Urutan eksekusi

- **Satu agent:** 4A → 4B → 4C → 4D → 4E → 4F → 4G.
- **Paralel:** 4A selesai; 4B + 4C paralel; 4D + 4E + 4F paralel; 4G setelah semua.
- **Aturan konflik:** 4A membuat semua shared extension point dan placeholder. 4B–4F hanya menambah file miliknya dan mengganti stub. Perubahan pada `MainWindow`, `SettingsWindow`, `IPlatformFactory`, atau `AppSettings` setelah 4A harus melalui integration task eksplisit.
- **Camera jangan diblokir oleh fitur lain.** 4F boleh dimulai paralel setelah 4A; backend V4L2 adalah long pole.

---

## 6. 4A — Foundation & Shell

### 6.1 Goal

Menghasilkan shell Avalonia yang secara visual dan perilaku setara WPF pada level shell/layout untuk permukaan yang sudah ada: compact pill, expanded Music shell, settings (General/Appearance/About), tray, minimal notch, dan window behavior. Detail konten Music (waveform, vinyl, accent) masuk 4B. 4A juga menyediakan seluruh fondasi yang dibutuhkan 4B–4F.

### 6.2 Scope

1. **Effect layer spike.**
   - Implementasi A1 dan/atau A2.
   - Gate: pointer nyata di 100/125/150/200%; klik di luar pill sampai ke window bawah; fokus tidak berpindah; glow/shadow terlihat; tidak flicker; RSS/CPU diukur.
   - Fallback inset effects jika gate gagal; keputusan dicatat.
2. **Design system.**
   - Token dari `Theme.xaml`: glass fill, solid fill, rim, top glow, specular, noise, shadow, lyrics card, toast, text hierarchy, accent colors, radii.
   - Inter sebagai font default; mapping weight Regular/Medium/SemiBold.
   - **Fluent UI System Icons** sebagai bundled icon font (MIT), dengan mapping codepoint untuk sekitar 20 glyph WPF. Deviasi bentuk ikon dicatat.
   - Custom control/gaya: glyph button, play button, tab button, toggle, card, scrollbar, toast, dock; battery widget dan equalizer. Chip/color swatch/text box dan mascot eyes menyusul di 4B/4C/4E.
3. **Composition dan contract.**
   - `AppServices`/`ShellComposition` membuat, memiliki, dan men-dispose service; `MainWindow` menjadi view/controller. Shutdown memakai `IAsyncDisposable`/`ShutdownAsync(timeout)` dan flush settings dengan batas waktu.
   - `IPlatformFactory` mendapat `CreateEffectLayer` dengan fallback `InsetEffectLayer`; service opsional (camera/audio/brightness/sound) ditambahkan oleh sub-project-nya dengan default interface method `null`, sehingga Windows tetap kompilasi.
   - `CapabilityMatrix` ditambah flag EffectLayer, Camera, Audio, Brightness, Lyrics, Shelf, Focus, Water, Mascot.
4. **Settings.**
   - `AppSettings` 22 field + `SchemaVersion=2`.
   - Migrasi v1; test default, roundtrip, dan clamp.
   - `SettingsWindow` shell: 6 nav item (General, Appearance, Lyrics, Focus, Water, About), custom chrome, card layout, scrollbar; General dan About fungsional; Appearance menampilkan layout penuh dengan toggle glass/bouncy/minimal dan Show mascot aktif/persist, sementara toggle overlay volume/brightness disabled sampai 4E; Lyrics/Focus/Water placeholder sampai sub-project-nya.
5. **Shell.**
   - Compact: clock, compact media, equalizer, mascot placeholder; battery/Wi-Fi/charging live in the expanded header.
   - Expanded host: header, Music view, dock 4 tab, chevron, toast host, lyrics pane placeholder.
   - Restyle expanded Music yang sudah ada; belum menambah lyrics/focus/camera/shelf.
   - Tray 8 item: Show/Hide, Start with system, Minimal notch, Show mascot, Hide in fullscreen, Settings, About, Exit. Show mascot tersimpan; animasinya masuk 4E. Refresh checkmark lewat `NativeMenu.NeedsUpdate`; tidak ada double-click di Avalonia 12.1.6.
   - Settings/About dialog custom (Avalonia tidak punya MessageBox).
6. **Window regression.**
   - Click-to-expand, hover, collapse delay, chevron, suppress-hover.
   - DPI 100/125/150/200 pada primary monitor; re-center on `Screens.Changed`. Following the monitor under the cursor (multi-monitor) ditunda ke 4G. Fullscreen hide, single instance, dan no-activate tetap diuji.
   - Effect layer visible/hidden sesuai state: hidden saat settings/glass off/user-hidden/fullscreen; minimal notch hanya shadow, ambient glow suppressed.

### 6.3 Non-goals

- Lyrics, Focus, Water, Shelf, Camera, overlay volume/brightness, mascot animation.
- Windows parity.
- Native Wayland.
- Installer, signing, auto-update.

### 6.4 Acceptance

- Capture side-by-side 125% untuk compact idle, compact media, expanded Music shell playing/paused/empty, settings General, settings Appearance, tray, minimal notch. WPF capture set adalah hard prerequisite; tanpa itu status visual = BLOCKED, bukan PASS.
- Semua perilaku L2 yang sudah ada tetap PASS, ditambah click-to-expand dan minimal-notch hover/click.
- Effect layer gate PASS atau fallback resmi dicatat; effect layer tidak mapped saat settings terbuka.
- `dotnet build` dan test suite PASS; test schema/AOT probe lama sudah diperbarui ke schema 2.
- GlyphMap test PASS (`SKTypeface.GetGlyph != 0`), tanpa tofu.
- NativeAOT publish tanpa trim warning; ready-file smoke PASS.
- Performance baseline 4A tercatat: idle RAM/CPU, expanded RAM/CPU, startup, binary.
- Tidak ada regresi WPF build.

### 6.5 Risks

- KWin menolak input shape/override-redirect effect window → fallback inset.
- Icon shapes tidak identik dengan Segoe Fluent Icons → deviasi visual.
- Settings schema migration mengubah banyak call site → test dan migrasi ketat.
- Shared-file churn saat 4B–4F → extension point dan integration task.

---

## 7. 4B — Music + Lyrics

### 7.1 Goal

Menyelesaikan tab Music dan lyrics agar setara WPF: artwork/vinyl, title/artist, waveform seek, accent dari artwork, source/footer, media recovery, dan lyrics pane.

### 7.2 Scope

- Perluas `MediaSnapshot`/`IMediaSession` dengan `TrackId`, `CanSeek`, `CanGoNext`, `CanGoPrevious`, dan event `PropertiesChanged`.
- Position resync dari MPRIS; interpolasi tetap halus; seek memakai `TrackId`.
- Waveform scrubber custom dengan hash track yang sama seperti WPF (FNV-1a).
- Vinyl berputar saat playing; pause menyimpan sudut; resume melanjutkan.
- Artwork accent: downscale 48 px, bucket HSL, fallback `#F6A6C8`; apply ke play button, waveform, equalizer, ambient glow.
- Footer: posisi/durasi, source app, playing dot.
- Media recovery: sesi MPRIS diganti/hilang, restart player, track change.
- Lyrics:
  - LRCLIB provider, cache lokal, cancellation.
  - Parser LRC dengan `[GeneratedRegex]`, offset, synced/plain/instrumental.
  - Pane 204 px, morph 300 ms, active line pill, fade atas/bawah, distance dimming, auto-scroll, click-to-seek.
  - Settings Lyrics: enabled, highlight color, swatch/hex.
- Settings page Lyrics fungsional.

### 7.3 Non-goals

- Camera, Shelf, Focus, Water, overlays, mascot.
- Lyrics provider selain LRCLIB.
- Native Wayland.

### 7.4 Acceptance

- Music playing/paused/empty/lyrics open.
- Seek via waveform; posisi benar setelah track change.
- Lyrics synced, plain, instrumental, offline/cache, dan error state.
- Highlight color valid/invalid/hex/swatch.
- Media restart recovery.
- Capture Music + lyrics 125%; AOT publish; test parser/cache/accent.

### 7.5 Risks

- MPRIS position sering stale → resync on `Seeked`/`PropertiesChanged`; toleransi drift.
- JSON/regex di NativeAOT → source-generated DTO dan `[GeneratedRegex]`.
- LRCLIB rate limit/offline → cache + fallback plain.

---

## 8. 4C — Focus + Water

### 8.1 Goal

Menambahkan Focus timer dan Water reminder persis seperti WPF, termasuk pill priority, header widget, toast, dan settings pages.

### 8.2 Scope

- Focus:
  - Service/timer 250 ms; phase Focus/ShortBreak/LongBreak; durations 1–180 menit; session counter; chime.
  - View: ring 144 px, chips, start/pause/reset, start break, session copy.
  - Pill: countdown menggantikan clock saat session berjalan; warna amber running/paused.
  - Settings Focus: 3 durasi + chime.
- Water:
  - Service: enabled, interval 1–240 menit, active hours, daily goal 1–20, glasses today, date rollover, suppression saat island hidden.
  - Header: 8 bars + counter, click untuk log.
  - Toast: reminder clickable, goal reached, glass logged.
  - Settings Water: toggle, interval, active hours, goal, preview/log/reset.
- Chime memakai `paplay`/`canberra` dengan WAV bundled; fallback senyap.

### 8.3 Non-goals

- Perubahan media, camera, shelf, overlay.
- Persist focus session (tetap session-only).
- Notifikasi sistem di luar toast in-window.

### 8.4 Acceptance

- Focus idle/running/paused/complete/start break; long break setiap 4 sesi; pill countdown.
- Water reminder, goal, active hours, date rollover, hidden suppression, click-to-log.
- Settings Focus/Water.
- Capture kedua fitur; test timer/date/schedule.

### 8.5 Risks

- Timer drift → `IClock`/monotonic time, test deterministik.
- Timezone/date rollover → invariant `yyyy-MM-dd`.
- Sering menulis settings → debounce, hanya saat nilai berubah.

---

## 9. 4D — Shelf

### 9.1 Goal

Menambahkan file shelf session-only dengan drag-in/out, batas 8 item, badge, status missing, dan drop overlay.

### 9.2 Scope

- Model shelf: path, nama, ekstensi, ukuran, exists; maksimal 8; duplicate detection.
- View 4×2 card 106×65; badge ekstensi berwarna; size label; missing opacity/label.
- Drag-in: seluruh island menerima file; auto-expand dan pindah ke tab Shelf; toast added/full/duplicate.
- Drag-out: `DoDragDropAsync` dengan `DataFormat.File`; pause collapse selama drag.
- Double-click membuka file via `xdg-open`.
- Blue dot di tab Shelf saat berisi item.
- Drop overlay dashed.
- Session-only; tidak ada persistensi.

### 9.3 Non-goals

- Copy file ke folder aplikasi; referensi path saja.
- Virtual file/FUSE.
- Persistensi shelf.

### 9.4 Acceptance

- Drag 1–8 file dari Dolphin; drag-out ke Dolphin dan Kate; double-click; clear.
- Full/duplicate/missing/empty states.
- Session-only setelah restart.
- Capture shelf empty/items/full; XDND test.

### 9.5 Risks

- XDND drag-out ke target tertentu tidak didukung → uji Dolphin/Kate, dokumentasikan.
- Path case-sensitive di Linux → duplicate detection `Ordinal`, bukan `OrdinalIgnoreCase`.
- Drag-out saat island collapse → suppression dan restore.

---

## 10. 4E — Overlays + Mascot

### 10.1 Goal

Menambahkan toast volume/brightness/charging dan mascot idle, dengan fallback yang aman saat backend tidak tersedia.

### 10.2 Scope

- Volume/mute: libpulse P/Invoke; event-driven; fallback `pactl subscribe`; setting `VolumeToast`.
- Brightness: read sysfs `actual_brightness`/`max_brightness`, poll 1 s; setting `BrightnessToast`; tidak menulis brightness di Phase 4.
- Charging: transisi battery charging → toast `Charging · N%`.
- Mascot: eyes 58×22, blink 2.6–5.6 s, doze ≥30 s, wake on hover/click; setting `ShowMascot`; compact clock fade.
- Settings Appearance: aktifkan toggle volume toast, brightness toast, dan mascot; minimal/glass/bouncy sudah aktif dari 4A.

### 10.3 Non-goals

- Kontrol volume/brightness dari app.
- DDC/CI eksternal.
- Native Wayland.

### 10.4 Acceptance

- Volume event <100 ms; mute/unmute; fallback pactl.
- Brightness poll menangkap perubahan <1.5 s; hide jika tidak ada backlight.
- Charging toast hanya pada transisi.
- Mascot idle/blink/doze/wake; toggle.
- Capture overlay/mascot; test fallback.

### 10.5 Risks

- libpulse callback AOT → `[UnmanagedCallersOnly]`, spike dulu.
- Sink/default device berubah → re-resolve index.
- Backlight tidak ada/eksternal → hide overlay, jangan crash.

---

## 11. 4F — Camera

### 11.1 Goal

Menambahkan tab Camera dengan preview live, mirror, switch camera, shutter full-resolution, error states, dan lifecycle yang sama seperti WPF.

### 11.2 Scope

- Backend V4L2:
  - Enumerasi `/dev/video*` dengan `VIDIOC_QUERYCAP`, filter capture node.
  - Preview MJPEG (target 640–1280 px), `mmap`, `poll`, ≤15 fps.
  - Decode MJPEG via Skia; `WriteableBitmap` update di UI.
  - Still: switch format ke resolusi maksimum, ambil frame MJPEG, tulis langsung sebagai JPEG; kembali ke preview.
  - Mirror hanya render transform; foto tidak mirror.
  - Error states: no camera, permission denied, busy, starting, live, error.
- View: preview 302×170, 16:9, LIVE chip, flash, mirror toggle, shutter, switch camera, retry/settings hint (tanpa `ms-settings`).
- Lifecycle: hanya aktif saat tab Camera terbuka, expanded, dan window visible; stop saat collapse/hide/fullscreen.
- Settings: `CameraMirror`; tidak ada camera settings page terpisah di WPF.
- Photo path: `XDG_PICTURES_DIR/Aster/Camera/camera-YYYYMMDD-HHmmss.jpg` (fallback `~/Pictures`).

### 11.3 Non-goals

- Video recording, microphone, upload.
- Multi-camera hotplug parity (nice-to-have; diuji jika device tersedia).
- PipeWire portal/Flatpak.

### 11.4 Acceptance

- V4L2 spike: 3 frame JPEG valid, resolusi maksimum, ≥12 fps preview.
- Preview live, mirror, shutter, file JPEG valid.
- No-camera/permission/busy fallback; tidak crash.
- Lifecycle stop saat tab pindah/collapse/hide.
- Capture camera live/error; AOT publish.

### 11.5 Risks

- Multi-camera/hotplug belum teruji → capability + refresh.
- Format switch menghentikan preview sebentar → UX yang disepakati.
- Decode MJPEG 1440p mahal → preview resolution rendah, still full-res.
- USB bandwidth/kamera UVC berbeda → error state dan fallback.

---

## 12. 4G — Verification & Release

### 12.1 Goal

Membuktikan paritas dan mengemas rilis Linux parity tanpa klaim yang berlebihan.

### 12.2 Scope

- Parity matrix per fitur/state: PASS/PARTIAL/NOT RUN/BLOCKED + evidence.
- Visual matrix: 100/125/150%, light/dark, compact/expanded/settings/tray/overlays/camera/shelf/focus/lyrics.
- Behavior matrix dari README WPF: hover, click, chevron, tab, lyrics, camera, shelf, focus, water, overlays, tray.
- AOT fungsional Linux: MPRIS, motion, settings, camera fallback, audio fallback setelah ready.
- Soak 60 menit: 100 expand/collapse, 20 track change; RSS/FD/handle.
- Accessibility: Orca/AT-SPI, keyboard, focus, automation names.
- Performance: idle/expanded/media, startup, binary; bandingkan budget.
- Packaging: Linux archive, native libs, README, notices, checksums.
- Docs: README/CHANGELOG/PLAN akurat; tidak ada klaim parity prematur.
- Release candidate: draft, smoke, publish manual.

### 12.3 Non-goals

- Fitur baru.
- Windows parity.
- Native Wayland.
- Installer/signing/auto-update.

### 12.4 Acceptance

- Semua gate 4A–4F PASS/PARTIAL dengan alasan.
- Tidak ada P0/P1 open.
- AOT tanpa trim warning.
- Soak/a11y/performance evidence.
- Paket dan dokumentasi siap.
- Tidak ada klaim “1:1” sebelum matrix selesai.

---

## 13. Arsitektur teknis bersama

### 13.1 Window dan effect layer

```text
┌─ effect window 760×460 DIP, transparan, no input ──────────┐
│   shadow · ambient glow · specular overflow                │
│        ┌─ island window 500×252, interaktif ─┐             │
│        │  panel content                     │             │
│        └────────────────────────────────────┘             │
└────────────────────────────────────────────────────────────┘
```

- Island window: pill-sized, topmost, no-activate, click-through di luar pill.
- Effect window: topmost, no focus, no input; di bawah island, di atas window biasa; disembunyikan saat settings/glass off/user-hidden/fullscreen; minimal notch hanya shadow, ambient glow suppressed.
- Effect layer mengikuti pusat island dan progress expand; konten statis per state, render ulang hanya saat accent/opacity berubah.
- `IEffectLayer` punya dua implementasi: X11 effect layer dan inset fallback.

### 13.2 Service split

| Layer | Isi |
|---|---|
| `Aster.Core` | Model, settings, `MediaProgress`, `SpringState`, `WindowGeometry`, `FocusService`, `WaterService`, `LyricsService`, `ShelfService`, `ArtworkAccent` |
| `Aster.UI.Abstractions` | `IPlatformFactory`, `IWindowChrome`, `IWindowPlacement`, `IEffectLayer`, capability |
| `Aster.Platform.Linux` | MPRIS, V4L2, libpulse, sysfs brightness, SNI tray, X11 chrome/placement, autostart, fullscreen, sound |
| `Aster.UI` | Views, view models, design system, shell, settings, tray commands |
| `Aster.Desktop.Linux` | Composition head, single instance, startup |

- `AppServices`/`ShellComposition` membuat dan men-dispose semua service; `MainWindow` hanya wiring view/event.
- `IPlatformFactory` menyediakan service opsional; capability matrix menentukan fitur yang tampil.
- Windows factory memakai stub unsupported sampai Phase 5.

### 13.3 Settings schema v2

| Grup | Field |
|---|---|
| Window/appearance | `StartWithWindows`, `MinimalNotch`, `HideInFullscreen`, `GlassBackground`, `BouncySpring`, `ShowMascot` |
| Toast | `VolumeToast`, `BrightnessToast` |
| Lyrics | `LyricsEnabled`, `LyricsHighlightColor` |
| Water | `WaterReminderEnabled`, `WaterIntervalMinutes`, `WaterActiveStartHour`, `WaterActiveEndHour`, `WaterDailyGoal`, `WaterGlassesToday`, `WaterDate` |
| Camera | `CameraMirror` |
| Focus | `FocusMinutes`, `ShortBreakMinutes`, `LongBreakMinutes`, `FocusChime` |
| Meta | `SchemaVersion` |

- Defaults/clamp mengikuti WPF.
- Migrasi v1 mempertahankan 5 field lama.
- Import WPF opsional; jangan mengklaim migrasi otomatis di Linux.

### 13.4 Threading, timer, lifecycle

- `ShellScheduler`/`IClock` ringan untuk cadence: 1 s clock, 250 ms progress, 4 s battery/network, 900 ms fullscreen, 2 s shelf, 1 s brightness.
- Camera di thread V4L2 sendiri; decode/scale di background; frame ≤15 fps ke UI.
- libpulse memakai `pa_threaded_mainloop`; callback marshal ke UI.
- Lyrics async + cache; tidak memblokir UI.
- `AppServices.Dispose` menghentikan camera/audio/timer/tray; flush settings dengan timeout.
- Ready file tetap ada; probe AOT Linux diperluas agar menjalankan MPRIS/motion/settings setelah ready.

### 13.5 Capability/fallback

| Kondisi | Perilaku |
|---|---|
| Camera tidak ada/denied | Empty/error state; tab tetap ada; tidak crash |
| libpulse tidak ada | Overlay volume disembunyikan; app tetap jalan |
| Backlight tidak ada | Overlay brightness disembunyikan |
| Lyrics gagal/offline | Plain lyrics atau empty state |
| Tray gagal | Settings terbuka otomatis; app tetap jalan |
| D-Bus/MPRIS mati | Media empty state |
| Fullscreen client Wayland native | Tidak terdeteksi; limitasi didokumentasikan |
| Effect layer gagal | Inset effects; deviasi tepi panel dicatat |

---

## 14. Referensi visual dan capture plan

Owner menyediakan capture WPF asli saat boot ke Windows. Capture ini adalah oracle untuk design parity, bukan screenshot README. `docs/results/phase4-reference/wpf/` belum ada; selama capture set belum tersedia, acceptance visual 4A berstatus `BLOCKED`, bukan PASS.

### 14.1 Lokasi

```text
docs/results/phase4-reference/wpf/
  info.md
  screenshots/<scale>-<wallpaper>/<state>.png
  videos/<flow>.mp4
  settings/<config>.json
```

`info.md`: Windows build, GPU, scale, wallpaper, commit/versi WPF, media source/title, tanggal, tool, catatan.

### 14.2 Prioritas capture

- **P0 — 4A:** compact idle, compact media playing/paused, expanded Music playing/paused/empty, settings General/Appearance, tray, minimal notch, hover/expand/chevron/tab videos.
- **P1 — 4B/4C/4D/4E/4F:** lyrics, focus, water, shelf, overlays, mascot, camera, glass off, bouncy on/off, fullscreen hide.
- **P2 — 4G:** matrix 100/125/150 + light/dark untuk compact, compact media, expanded Music, lyrics, settings General, tray; 200% opsional.

### 14.3 Aturan capture

- Snipping Tool mode *Window* atau `Win+Shift+S`; sertakan island + margin 40–60 px.
- Jangan `Alt+PrintScreen` untuk window transparan.
- Video 60 fps, 5–15 detik, untuk hover/tab/lyrics/bouncy/mascot.
- Media fixture stabil; catat title/artist/artwork/source.
- Jika fitur tidak bisa di-capture (no camera, brightness tidak didukung), capture error state dan catat; jangan dipalsukan.

---

## 15. Target dan gate

### 15.1 Performance budget

| Metrik | Target | Masih diterima |
|---|---:|---:|
| RAM idle | ~150 MB | ≤180 MB |
| RAM expanded + media | ~220 MB | ≤260 MB |
| CPU idle | ~1% | ≤2% |
| CPU animasi | ~3% | ≤5% |
| Startup | ~1 detik | ≤1,5 detik |
| Binary | ~50 MB | ≤70 MB |

Baseline Linux Phase 2: 129 MB RSS, 25 MB binary, ~100 ms startup. Efek glass/glow dan camera harus diukur ulang; jangan mengasumsikan budget otomatis tercapai.

### 15.2 Status dan klaim

- Status hanya `PASS`, `PARTIAL`, `NOT RUN`, `BLOCKED`; deferred memakai `DEFERRED`.
- `PARTIAL`/`NOT RUN` tidak dihitung PASS.
- Klaim “1:1” atau “parity” hanya setelah 4G matrix selesai.
- Phase 4 tidak menutup waiver Phase 1 atau gate Phase 2; keduanya tetap tercatat di register lama. Phase 4 punya parity matrix sendiri.
- Target rilis internal Phase 4 adalah `0.12.0` Linux parity preview; nama dan nomor final diputuskan di 4G.
- Windows parity tidak diklaim di Phase 4.
- Native Wayland tidak diklaim.
- macOS deferred.

---

## 16. Risiko utama

| Risiko | Dampak | Mitigasi |
|---|---|---|
| Effect layer ditolak KWin | Glow/shadow hilang | Spike A1/A2; fallback inset; `IEffectLayer` |
| Camera V4L2 multi-device/hotplug | Fitur tidak lengkap | Capability + refresh; uji device yang ada; dokumentasikan |
| libpulse callback AOT | Crash/event hilang | `[UnmanagedCallersOnly]`; spike; fallback pactl |
| Brightness eksternal/DDC | Overlay tidak muncul | Read-only sysfs; hide jika tidak ada; DDC opsional |
| XDND drag-out target tertentu | Drag-out gagal | Uji Dolphin/Kate; dokumentasikan batas |
| Fullscreen klien Wayland native | Island tidak hide | X11-only claim; KWin script opsional |
| Icon font berbeda | Visual tidak identik | Fluent UI System Icons; deviasi dicatat |
| Efek glass/animasi menaikkan CPU/RAM | Performa turun | Pre-bake shadow/glow; gate timer; ukur tiap sub-project |
| Shared file conflict saat paralel | Integrasi kacau | Extension point 4A; integration task |
| Settings migration | Nilai user hilang | Schema v2 + test + backup |
| Docs lokal ter-commit | Melanggar aturan repo | Stage path spesifik; cek `git status` |

---

## 17. Checkpoint

1. **4A shell** — compact + expanded Music + settings + tray tampil setara WPF; effect layer diputuskan.
2. **4B music** — Music + lyrics lengkap; media recovery.
3. **4C focus/water** — timer, reminder, header, settings.
4. **4D shelf** — drag-in/out, 8 item, badges.
5. **4E overlays/mascot** — volume/brightness/charging, mascot.
6. **4F camera** — V4L2 preview/still, lifecycle.
7. **4G release** — matrix, AOT, soak, a11y, performance, package.

---

## 18. Langkah berikutnya

1. Selesaikan Phase 3: merge PR #1, publish v0.11.0, smoke archive.
2. Spec **4A** disetujui (`docs/specs/2026-11-17-aster-phase-4a-shell-design.md`).
3. Review implementation plan **4A Part 1** (`docs/plans/2026-11-17-aster-phase-4a.md`) dan **Part 2** (`docs/plans/2026-11-17-aster-phase-4a-shell.md`), lalu eksekusi Part 1.
4. Implementasi 4A dan tutup gate-nya.
5. Setelah 4A stabil, tulis spec/plan 4B–4F; jalankan sesuai dependency.
6. Jalankan 4G dan publish Linux parity candidate.
7. Capture WPF P0 dapat dimulai sekarang saat boot ke Windows; capture P1/P2 menyusul per sub-project.

Tidak ada kode Phase 4 sebelum spec dan plan 4A disetujui.

---

## 19. Arsip

Dokumen dan bukti fase lama tetap tersedia (local-only):

- `docs/specs/2026-11-14-aster-cross-platform-design.md`
- `docs/specs/2026-11-14-aster-phase-1-design.md`
- `docs/specs/2026-11-16-aster-phase-2-design.md`
- `docs/specs/2026-11-16-aster-phase-3-design.md`
- `docs/plans/2026-11-14-aster-phase-0.md`
- `docs/plans/2026-11-14-aster-phase-1.md`
- `docs/plans/2026-11-16-aster-phase-2.md`
- `docs/plans/2026-11-16-aster-phase-3.md`
- `docs/results/phase0-report.md`
- `docs/results/phase1-report.md`
- `docs/results/phase2-report.md`
- `docs/results/phase3-gap-register.md`
- `docs/adr/0001-distribution-baseline.md`

Riwayat Git menyimpan versi `PLAN.md` sebelumnya. Dokumen ini adalah rencana eksekusi Phase 4, bukan pengganti bukti fase 0–3.
