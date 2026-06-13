/* JonnXor — Tweaks panel mount (theme, glow, CRT scanlines) */
const JX_TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "theme": "rune",
  "glow": 1,
  "scanlines": true
}/*EDITMODE-END*/;

function JXTweaks() {
  const [t, setTweak] = useTweaks(JX_TWEAK_DEFAULTS);

  // Theme is applied only on explicit user change (see TweakRadio onChange) so a
  // restored tweak value can never override the site's own nav toggle.
  React.useEffect(() => {
    const cur = window.JX && window.JX.getTheme();
    if (cur && cur !== t.theme) setTweak('theme', cur);
  }, []);
  React.useEffect(() => { window.JX && window.JX.setGlow(t.glow); }, [t.glow]);
  React.useEffect(() => { window.JX && window.JX.setScanlines(t.scanlines); }, [t.scanlines]);

  return (
    <TweaksPanel>
      <TweakSection label="Theme" />
      <TweakRadio
        label="Mode"
        value={t.theme}
        options={['dawn', 'rune', 'neon']}
        onChange={(v) => { setTweak('theme', v); window.JX && window.JX.setTheme(v); }}
      />
      <TweakSection label="Atmosphere" />
      <TweakSlider
        label="Glow strength"
        value={t.glow}
        min={0}
        max={2}
        step={0.1}
        onChange={(v) => setTweak('glow', v)}
      />
      <TweakToggle
        label="CRT scanlines (Neon)"
        value={t.scanlines}
        onChange={(v) => setTweak('scanlines', v)}
      />
    </TweaksPanel>
  );
}

(function mountJXTweaks() {
  const host = document.createElement('div');
  host.id = 'jx-tweaks-root';
  document.body.appendChild(host);
  ReactDOM.createRoot(host).render(<JXTweaks />);
})();
