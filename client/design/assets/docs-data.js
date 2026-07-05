/* JonnXor — Grimoire content.
   Each doc: id, realm ('games'|'code'|'life'), game (games realm only),
   cat, title, tags, updated, body (HTML with h2 sections → TOC).
   Placeholder entries — replace with real docs as they're written. */
window.JX_DOCS = [

  /* ---------------- Cheat codes ---------------- */
  {
    id: 'classic-codes',
    realm: 'games',
    game: 'Classics',
    cat: 'Cheat codes',
    title: 'Classic codes every gamer should know',
    tags: ['history', 'consoles'],
    updated: '2026-06-02',
    body:
      '<p>The old incantations. Some still work, most live on as cultural runes — but a developer who doesn\u2019t know the Konami code is like a skald who can\u2019t name Odin.</p>' +
      '<h2>The canon</h2>' +
      '<table class="doc-table"><thead><tr><th>Incantation</th><th>Game</th><th>Effect</th></tr></thead><tbody>' +
      '<tr><td>↑ ↑ ↓ ↓ ← → ← → B A</td><td>Contra (NES)</td><td>30 lives — the Konami code itself</td></tr>' +
      '<tr><td>IDDQD</td><td>Doom</td><td>God mode</td></tr>' +
      '<tr><td>IDKFA</td><td>Doom</td><td>All weapons, keys, full ammo</td></tr>' +
      '<tr><td>justin bailey</td><td>Metroid (NES)</td><td>Samus without the suit, fully kitted</td></tr>' +
      '<tr><td>HESOYAM</td><td>GTA: San Andreas</td><td>Health, armor, $250k</td></tr>' +
      '<tr><td>motherlode</td><td>The Sims</td><td>§50,000 — the honest economy</td></tr>' +
      '</tbody></table>' +
      '<h2>House rules</h2>' +
      '<div class="tip"><b>House rule</b>Cheat codes in single-player are a save-rescue tool and a museum exhibit, never a first playthrough. Souls games have no codes. That is the point of Souls games.</div>',
  },

  /* ---------------- Walkthroughs ---------------- */
  {
    id: 'gascoigne',
    realm: 'games',
    game: 'Bloodborne',
    cat: 'Walkthroughs',
    title: 'Bloodborne: surviving Father Gascoigne',
    tags: ['boss'],
    updated: '2026-05-21',
    body:
      '<p>The first true wall in Bloodborne, and the lesson-teacher: aggression is defense. Written for new hunters who keep meeting the fog wall.</p>' +
      '<h2>Before the fight</h2>' +
      '<ul>' +
      '<li>Grab the <strong>Music Box</strong> from the little girl in the window (Central Yharnam, after the bridge shortcut). It staggers him three times.</li>' +
      '<li>Farm the two brick trolls near the lamp if you\u2019re under level 15 — echoes spent are echoes saved.</li>' +
      '<li>Bring <strong>oil urns + Molotovs</strong> for phase 3.</li>' +
      '</ul>' +
      '<h2>The three phases</h2>' +
      '<ol>' +
      '<li><strong>Hunter (axe + pistol):</strong> stay close and circle left. His gun only matters at range. Parry windows on the two-hand slam are generous.</li>' +
      '<li><strong>Extended axe:</strong> he gains reach and poise. Use the tombstones as cover — he destroys them, you don\u2019t care, it costs him the swing.</li>' +
      '<li><strong>Beast:</strong> drop the Music Box, run in, unload everything. He staggers off headshots mid-leap. The fountain area is the safest arena.</li>' +
      '</ol>' +
      '<h2>If you\u2019re still stuck</h2>' +
      '<div class="tip"><b>Hesitation is defeat</b>Healing at range invites the leap. Heal <em>while</em> circling, never while backpedaling in a straight line.</div>',
  },
  {
    id: 'limgrave-route',
    realm: 'games',
    game: 'Elden Ring',
    cat: 'Walkthroughs',
    title: 'Elden Ring: a Limgrave route that respects your time',
    tags: ['route', 'open world'],
    updated: '2026-06-05',
    body:
      '<p>The long-form one. Limgrave is the best-designed open zone FromSoftware ever shipped, and also where most new Tarnished waste ten hours running past the good parts. This route gets you to Stormveil strong, informed, and properly equipped — without following a checklist so rigid it stops being an adventure.</p>' +
      '<div class="tip"><b>Spoiler policy</b>Locations and item names are named; story beats and surprises are not. Trust the route, keep the wonder.</div>' +
      '<h2>The first hour: ignore the glowing boss</h2>' +
      '<p>The Tree Sentinel at the gate is a tutorial disguised as a wall: the lesson is <em>you do not have to fight anything</em>. Ride past. Your first three stops:</p>' +
      '<ol>' +
      '<li><strong>Church of Elleh</strong> — merchant, crafting kit, and (return at night) the start of a sorcery questline worth having.</li>' +
      '<li><strong>Gatefront Ruins</strong> — the map fragment (by the road gate, you cannot miss the stele) and the <strong>whetstone knife</strong> in the cellar. Ashes of war change everything; get this before any fight you care about.</li>' +
      '<li><strong>The Stranded Graveyard hole you skipped</strong> — go back down. The tutorial cave teaches the guard counter, and guard counters carry casual playthroughs.</li>' +
      '</ol>' +
      '<h2>Gear that trivializes the early game</h2>' +
      '<table class="doc-table"><thead><tr><th>Item</th><th>Where</th><th>Why</th></tr></thead><tbody>' +
      '<tr><td>Lordsworn\u2019s Greatsword</td><td>Gatefront camp, the carriage chest</td><td>Best generic STR blade for 20 hours</td></tr>' +
      '<tr><td>Flask of Wondrous Physick</td><td>Third Church of Marika (east road)</td><td>A free second heal, customizable</td></tr>' +
      '<tr><td>Spirit Calling Bell</td><td>Church of Elleh, at night</td><td>Summons. Use them. It is not cheating; it is the design.</td></tr>' +
      '<tr><td>Golden Seed ×2</td><td>Glowing sapling by Stormgate; Fort Haight road</td><td>+2 flask charges before the first real boss</td></tr>' +
      '</tbody></table>' +
      '<h2>The detours that are actually mandatory</h2>' +
      '<p>Not literally — but skipping these is how people arrive at Margit underleveled and blame the game:</p>' +
      '<ul>' +
      '<li><strong>Groveside Cave</strong> (5 min): the Flamedrake talisman. Fire resistance pays for the whole zone.</li>' +
      '<li><strong>Stormfoot Catacombs</strong> (15 min): early imp-seal practice and a useful ash.</li>' +
      '<li><strong>Agheel Lake at night</strong> (5 min): a certain flying someone sleeps there. You don\u2019t have to win; you have to <em>see</em> it. Limgrave\u2019s scale clicks afterwards.</li>' +
      '<li><strong>Mistwood</strong>: listen for howling, then talk to the merchant at Elleh. You\u2019ll know why later.</li>' +
      '</ul>' +
      '<h2>Leveling: where the smoothbrain souls are</h2>' +
      '<p>If you want to over-level honestly: the war camp south of Agheel Lake resets fast and the soldiers die to two guard counters each. Twenty minutes here and Margit becomes a fair fight. Vigor first — every death under 20 Vigor is a stat-allocation error wearing a boss\u2019s costume.</p>' +
      '<h2>Margit, the checklist</h2>' +
      '<ol>' +
      '<li>Two Golden Seeds spent (4+ flasks).</li>' +
      '<li>Wondrous Physick mixed (crimson + whatever suits your build).</li>' +
      '<li>An ash of war on your weapon, weapon at +2 or +3.</li>' +
      '<li><strong>Margit\u2019s Shackle</strong> bought from a certain patches-adjacent merchant in Murkwater Cave — two free knockdowns.</li>' +
      '<li>Jellyfish or Wolves summoned <em>before</em> the fog, at the stairs\u2019 base.</li>' +
      '</ol>' +
      '<p>He punishes panic-rolling and rewards patience: walk during his combos, strafe the staff slam, punish the jump. Put these foolish ambitions to rest — his, not yours.</p>' +
      '<h2>What NOT to do yet</h2>' +
      '<ul>' +
      '<li>Don\u2019t go east past the bridge into Caelid because a dog chased you there. You will know Caelid when you see it. Leave.</li>' +
      '<li>Don\u2019t spend runes on arrows and pots before Vigor 20.</li>' +
      '<li>Don\u2019t open the sending-gate behind the Third Church unless you enjoy level 1 visits to endgame zones. (Do it once, obviously. Then leave.)</li>' +
      '</ul>',
  },
  {
    id: 'dragons-dogma-start',
    realm: 'games',
    game: 'Dragon\u2019s Dogma',
    cat: 'Walkthroughs',
    title: 'Dragon\u2019s Dogma 2: first 10 hours without regrets',
    tags: ['guide'],
    updated: '2026-04-28',
    body:
      '<p>Aught taken is aught earned. A spoiler-light path through the opening so you don\u2019t lock yourself out of the good stuff.</p>' +
      '<h2>The five rules</h2>' +
      '<ol>' +
      '<li><strong>Vocation:</strong> start Fighter or Archer; Trickster is a second-playthrough class no matter what your heart says.</li>' +
      '<li><strong>Main pawn:</strong> make them the opposite of you. Melee Arisen → Mage pawn. You can respec later; their early inclination sticks around.</li>' +
      '<li><strong>Do not rush Vernworth.</strong> The road events between Melve and the capital are the tutorial the game refuses to label.</li>' +
      '<li><strong>Port crystals are precious.</strong> Don\u2019t place one until you\u2019ve seen the volcanic island.</li>' +
      '<li><strong>Sleep at inns</strong>, not camps, when you\u2019ve done anything important — inn saves are your only real checkpoint.</li>' +
      '</ol>' +
      '<h2>On pawns</h2>' +
      '<div class="tip"><b>Pawn chatter</b>Yes, they never stop talking. No, you can\u2019t fully turn it off. They\u2019re masterworks all; you can\u2019t go wrong.</div>',
  },

  /* ---------------- Cheat sheets ---------------- */
  {
    id: 'git-incantations',
    realm: 'code',
    cat: 'Cheat sheets',
    title: 'Git incantations for when things go wrong',
    tags: ['git', 'recovery'],
    updated: '2026-05-30',
    body:
      '<p>The panic-page. Bookmark this, breathe, and remember: if it was ever committed, it is almost never lost.</p>' +
      '<h2>Undo, by severity</h2>' +
      '<table class="doc-table"><thead><tr><th>Spell</th><th>When</th></tr></thead><tbody>' +
      '<tr><td>git restore .</td><td>Uncommitted mess, want it gone</td></tr>' +
      '<tr><td>git reset --soft HEAD~1</td><td>Committed too early, keep the work staged</td></tr>' +
      '<tr><td>git commit --amend</td><td>Typo in the message / forgot one file</td></tr>' +
      '<tr><td>git revert &lt;sha&gt;</td><td>Bad commit already pushed — undo in public, honestly</td></tr>' +
      '<tr><td>git reflog</td><td>\u201cI have destroyed everything\u201d — you haven\u2019t; find the sha, reset to it</td></tr>' +
      '</tbody></table>' +
      '<h2>The interactive rebase ritual</h2>' +
      '<pre class="code"><span class="cm"># rewrite the last 4 commits \u2014 NEVER on a shared branch</span>\n' +
      'git rebase -i HEAD~4\n' +
      '<span class="cm"># pick \u00b7 reword \u00b7 squash \u00b7 fixup \u00b7 drop \u2014 in the editor</span>\n' +
      '<span class="cm"># abort from inside the storm:</span>\n' +
      'git rebase --abort</pre>' +
      '<div class="tip"><b>Loki\u2019s law</b>Force-push only with <kbd>--force-with-lease</kbd>. Tricksters survive by leaving an exit.</div>',
  },
  {
    id: 'css-grid-runes',
    realm: 'code',
    cat: 'Cheat sheets',
    title: 'CSS Grid runes',
    tags: ['css', 'layout'],
    updated: '2026-05-12',
    body:
      '<p>The five lines that solve 90% of layouts, and the two that solve the rest.</p>' +
      '<h2>The five lines</h2>' +
      '<pre class="code"><span class="cm">/* the workhorse: responsive card grid, no media query */</span>\n' +
      '.grid { display: grid;\n' +
      '  grid-template-columns: <span class="fn">repeat</span>(auto-fill, <span class="fn">minmax</span>(<span class="nu">240px</span>, <span class="nu">1fr</span>));\n' +
      '  gap: <span class="nu">20px</span>; }\n\n' +
      '<span class="cm">/* the holy centering rune */</span>\n' +
      '.center { display: grid; place-items: center; }\n\n' +
      '<span class="cm">/* sidebar + content, sidebar fixed */</span>\n' +
      '.shell { display: grid; grid-template-columns: <span class="nu">280px</span> <span class="nu">1fr</span>; }\n\n' +
      '<span class="cm">/* overlap without position: absolute */</span>\n' +
      '.stack &gt; * { grid-area: <span class="nu">1</span> / <span class="nu">1</span>; }</pre>' +
      '<h2>Disambiguation runes</h2>' +
      '<table class="doc-table"><thead><tr><th>Rune</th><th>Meaning</th></tr></thead><tbody>' +
      '<tr><td>auto-fill vs auto-fit</td><td>fill keeps empty tracks; fit collapses them (stretching the rest)</td></tr>' +
      '<tr><td>minmax(0, 1fr)</td><td>the \u201cwhy is my grid blowing out\u201d fix — allows content to shrink</td></tr>' +
      '<tr><td>subgrid</td><td>child rows/cols align to the parent\u2019s tracks — card internals, finally aligned</td></tr>' +
      '</tbody></table>',
  },
  {
    id: 'jlpt-n4',
    realm: 'life',
    cat: 'Cheat sheets',
    title: '日本語: particles I keep mixing up',
    tags: ['japanese', 'study'],
    updated: '2026-06-08',
    body:
      '<p>Personal N4-climbing notes. は vs が is a lifelong boss fight; these are the openings I\u2019ve found.</p>' +
      '<h2>The particle table</h2>' +
      '<table class="doc-table"><thead><tr><th>Particle</th><th>Job</th><th>Hook</th></tr></thead><tbody>' +
      '<tr><td>は</td><td>topic — \u201cas for X\u2026\u201d</td><td>spotlight on what comes <em>after</em></td></tr>' +
      '<tr><td>が</td><td>subject — new info, emphasis</td><td>spotlight on what comes <em>before</em></td></tr>' +
      '<tr><td>に</td><td>destination, time, existence</td><td>a pin on the map</td></tr>' +
      '<tr><td>で</td><td>location of action, means</td><td>the stage, the tool</td></tr>' +
      '<tr><td>を</td><td>direct object</td><td>the thing the verb eats</td></tr>' +
      '</tbody></table>' +
      '<h2>Mnemonics</h2>' +
      '<div class="tip"><b>Mnemonic</b>図書館<strong>で</strong>勉強する (study <em>at</em> the library — stage) vs 図書館<strong>に</strong>行く (go <em>to</em> the library — pin).</div>',
  },

  /* ---------------- Code patterns ---------------- */
  {
    id: 'go-errors',
    realm: 'code',
    cat: 'Code patterns',
    title: 'Go error wrapping, the saga pattern',
    tags: ['go', 'errors'],
    updated: '2026-05-26',
    body:
      '<p>Every error should read like a saga line: who attempted what, and what stood in the way. Wrap with context at every boundary; never log <em>and</em> return.</p>' +
      '<h2>The pattern</h2>' +
      '<pre class="code"><span class="kw">func</span> (s *Store) <span class="fn">LoadSaga</span>(ctx context.Context, id <span class="kw">string</span>) (*Saga, <span class="kw">error</span>) {\n' +
      '    row, err := s.db.<span class="fn">QueryRow</span>(ctx, qSaga, id)\n' +
      '    <span class="kw">if</span> err != <span class="kw">nil</span> {\n' +
      '        <span class="cm">// wrap: add the chapter, keep the cause</span>\n' +
      '        <span class="kw">return</span> <span class="kw">nil</span>, fmt.<span class="fn">Errorf</span>(<span class="st">"load saga %s: %w"</span>, id, err)\n' +
      '    }\n' +
      '    <span class="kw">return</span> row, <span class="kw">nil</span>\n' +
      '}\n\n' +
      '<span class="cm">// at the top of the world, choose the reaction once:</span>\n' +
      '<span class="kw">if</span> errors.<span class="fn">Is</span>(err, pgx.ErrNoRows) { <span class="kw">return</span> http.StatusNotFound }\n' +
      '<span class="kw">var</span> qe *QuotaError\n' +
      '<span class="kw">if</span> errors.<span class="fn">As</span>(err, &amp;qe) { <span class="kw">return</span> http.StatusTooManyRequests }</pre>' +
      '<h2>The three rules</h2>' +
      '<ul>' +
      '<li><strong>Wrap with <code class="inline">%w</code></strong> at every layer boundary — repository, service, handler.</li>' +
      '<li><strong>Decide once</strong>: the outermost layer maps errors to status codes / messages. Inner layers never print.</li>' +
      '<li><strong>Sentinel errors are public API.</strong> Export them deliberately or not at all.</li>' +
      '</ul>',
  },
  {
    id: 'react-fetch',
    realm: 'code',
    cat: 'Code patterns',
    title: 'React: the fetch-on-mount I actually ship',
    tags: ['react', 'typescript'],
    updated: '2026-04-19',
    body:
      '<p>No library, no waterfall surprises, no setting state on unmounted components. The boring, sturdy version.</p>' +
      '<h2>The hook</h2>' +
      '<pre class="code"><span class="kw">function</span> <span class="fn">useQuest</span>(id: <span class="kw">string</span>) {\n' +
      '  <span class="kw">const</span> [state, setState] = React.<span class="fn">useState</span>&lt;State&gt;({ kind: <span class="st">"loading"</span> });\n\n' +
      '  React.<span class="fn">useEffect</span>(() =&gt; {\n' +
      '    <span class="kw">const</span> ctrl = <span class="kw">new</span> <span class="fn">AbortController</span>();\n' +
      '    <span class="fn">fetchQuest</span>(id, ctrl.signal)\n' +
      '      .<span class="fn">then</span>((q) =&gt; <span class="fn">setState</span>({ kind: <span class="st">"ok"</span>, quest: q }))\n' +
      '      .<span class="fn">catch</span>((e) =&gt; {\n' +
      '        <span class="kw">if</span> (!ctrl.signal.aborted) <span class="fn">setState</span>({ kind: <span class="st">"error"</span>, error: e });\n' +
      '      });\n' +
      '    <span class="kw">return</span> () =&gt; ctrl.<span class="fn">abort</span>(); <span class="cm">// the cleanup IS the pattern</span>\n' +
      '  }, [id]);\n\n' +
      '  <span class="kw">return</span> state;\n' +
      '}</pre>' +
      '<h2>Why this shape</h2>' +
      '<ul>' +
      '<li>State is a <strong>tagged union</strong> (<code class="inline">loading | ok | error</code>) — impossible states stay impossible.</li>' +
      '<li>The <strong>AbortController teardown</strong> handles both unmount and id changes in one move.</li>' +
      '<li>Upgrade path: same shape drops straight into TanStack Query when caching matters.</li>' +
      '</ul>',
  },

  /* ---------------- Guides ---------------- */
  {
    id: 'chai',
    realm: 'life',
    cat: 'Guides',
    title: 'Masala chai for debugging sessions',
    tags: ['chai', 'ritual'],
    updated: '2026-06-01',
    body:
      '<p>Batch #214 of the house potion. Brewed before every gnarly bug since 2019. Serves two, or one engineer facing a heisenbug.</p>' +
      '<h2>Components</h2>' +
      '<ul>' +
      '<li>2 cups water · 1 cup whole milk</li>' +
      '<li>2 tsp loose Assam (strong, honest, CTC)</li>' +
      '<li>6 green cardamom pods, <strong>crushed</strong> — this is non-negotiable</li>' +
      '<li>4 cloves · 1 cinnamon stick · thumb of ginger, smashed</li>' +
      '<li>black pepper, 4–5 cracks (the mischief)</li>' +
      '<li>sugar to taste — chai without any is a missed save point</li>' +
      '</ul>' +
      '<h2>Ritual</h2>' +
      '<ol>' +
      '<li>Simmer spices in water 5 min — until the kitchen smells like a better codebase.</li>' +
      '<li>Add tea, 2 min. No more — Assam turns bitter like an unreviewed PR.</li>' +
      '<li>Add milk + sugar, bring <em>just</em> to the boil, then pull it back. Twice.</li>' +
      '<li>Strain, pour from height for foam, return to the debugger.</li>' +
      '</ol>' +
      '<h2>Protocols</h2>' +
      '<div class="tip"><b>Cat protocol</b>The cat gets to sniff the steam, never the cup. The bug is usually in the code you were sure about; check it after the first sip, not before.</div>',
  },
  {
    id: 'wsl-setup',
    realm: 'code',
    cat: 'Guides',
    title: 'My WSL dev environment, from zero',
    tags: ['wsl', 'setup'],
    updated: '2026-06-10',
    body:
      '<p>Gaming PC by night, dev rig by day. The exact order that avoids the usual traps.</p>' +
      '<h2>The order</h2>' +
      '<ol>' +
      '<li><kbd>wsl --install -d Ubuntu</kbd> in an admin terminal, reboot, create the unix user.</li>' +
      '<li><strong>Keep code inside WSL</strong> (<code class="inline">~/dev</code>), never on <code class="inline">/mnt/c</code> — file watching and git are 10× faster.</li>' +
      '<li>Install via apt: <code class="inline">build-essential git curl unzip</code>; then mise (or asdf) for Node/Go/Python versions.</li>' +
      '<li>Git credentials: <kbd>git config --global credential.helper "/mnt/c/Program\\ Files/Git/mingw64/bin/git-credential-manager.exe"</kbd> — one credential store for both worlds.</li>' +
      '<li>Editor connects <em>into</em> WSL (VS Code Remote / JetBrains Gateway); terminals live in Windows Terminal with a proper Nerd Font.</li>' +
      '<li>Claude Code runs inside the WSL shell, next to the code. Hand it a README and a design system and let it cook.</li>' +
      '</ol>' +
      '<h2>When it misbehaves</h2>' +
      '<div class="tip"><b>Memory rune</b>If WSL eats your RAM, cap it in <code class="inline">%UserProfile%\\.wslconfig</code>: <kbd>[wsl2]</kbd> then <kbd>memory=8GB</kbd>. Vmmem will thank you.</div>',
  }
];
