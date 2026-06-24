/* ============================================================
   JonnXor — shared site chrome & behavior
   Renders nav + footer, theme switcher (sun/moon/dragon),
   language switcher, countdown utils, reveal, dragonfly egg.
   ============================================================ */
(function () {
  'use strict';

  var THEMES = ['dawn', 'rune', 'neon'];

  var ICONS = {
    search: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5 L21 21"/></svg>',
    sun: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="12" cy="12" r="4.2" fill="currentColor" stroke="none"/><path d="M12 2.5v2.4M12 19.1v2.4M2.5 12h2.4M19.1 12h2.4M5.2 5.2l1.7 1.7M17.1 17.1l1.7 1.7M18.8 5.2l-1.7 1.7M6.9 17.1l-1.7 1.7"/></svg>',
    moon: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>',
    dragon: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M21 11 L8 5 L3 1 L7 8 L4 9 L8 17 L14 14 L12 11 Z"/></svg>',
    home: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"><path d="M3 11 L12 3 L21 11 V21 H14 V15 H10 V21 H3 Z"/></svg>',
    hammer: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7 L13 7 L13 3 L17 3 L20 6 L20 10 L13 10 L13 11 L4 11 Z"/><path d="M8.5 11 L8.5 21"/></svg>',
    book: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 4 H10 C11.1 4 12 4.9 12 6 V20 C12 19 11 18.5 10 18.5 H4 Z"/><path d="M20 4 H14 C12.9 4 12 4.9 12 6 V20 C12 19 13 18.5 14 18.5 H20 Z"/></svg>',
    pad: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M7 8 H17 C19.5 8 21 10 21 13 C21 16 19.8 17.5 18.4 17.5 C16.5 17.5 16.4 15 14.5 15 H9.5 C7.6 15 7.5 17.5 5.6 17.5 C4.2 17.5 3 16 3 13 C3 10 4.5 8 7 8 Z"/><path d="M8 10.6 V13.4 M6.6 12 H9.4 M15.6 11 h.01 M17.4 13 h.01"/></svg>',
    dots: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="19" cy="12" r="1.8"/></svg>',
    dfly: '<svg width="46" height="46" viewBox="0 0 46 46" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"><path d="M23 12 L23 36" stroke-width="2.4"/><circle cx="23" cy="10" r="2.6" fill="currentColor" stroke="none"/><path d="M21 17 C8 10, 4 14, 9 19 C13 23, 19 20, 21 19 Z" fill="currentColor" stroke="none" opacity=".55"/><path d="M25 17 C38 10, 42 14, 37 19 C33 23, 27 20, 25 19 Z" fill="currentColor" stroke="none" opacity=".55"/><path d="M21 22 C10 19, 6 23, 11 26 C15 28, 19 24, 21 23 Z" fill="currentColor" stroke="none" opacity=".35"/><path d="M25 22 C36 19, 40 23, 35 26 C31 28, 27 24, 25 23 Z" fill="currentColor" stroke="none" opacity=".35"/></svg>'
  };

  var PAGES = [
    { id: 'home', label: 'Home', href: '/' },
    { id: 'about', label: 'About', href: '/about' },
    { id: 'cv', label: 'CV', href: '/cv' },
    { id: 'portfolio', label: 'Portfolio', href: '/portfolio' },
    { id: 'blog', label: 'Blog', href: '/blog' },
    { id: 'docs', label: 'Docs', href: '/docs' },
    { id: 'games', label: 'Games', href: '/games' },
    { id: 'wallpapers', label: 'Wallpapers', href: '/wallpapers' },
    { id: 'countdowns', label: 'Countdowns', href: '/countdowns' }
  ];

  function store(key, val) { try { localStorage.setItem(key, val); } catch (e) {} }
  function read(key) { try { return localStorage.getItem(key); } catch (e) { return null; } }

  /* ---------- theme ---------- */
  function getTheme() {
    return document.documentElement.getAttribute('data-theme') || 'rune';
  }
  function setTheme(t) {
    if (THEMES.indexOf(t) === -1) return;
    document.documentElement.setAttribute('data-theme', t);
    store('jx-theme', t);
    document.querySelectorAll('.theme-toggle button').forEach(function (b) {
      b.classList.toggle('active', b.getAttribute('data-set') === t);
    });
    var orb = document.querySelector('.nav-orb');
    if (orb) {
      var names = { dawn: 'Dawn (light)', rune: 'Rune Night (dark)', neon: 'Neon (gamer)' };
      orb.innerHTML = ICONS[{ dawn: 'sun', rune: 'moon', neon: 'dragon' }[t]];
      orb.setAttribute('aria-label', 'Theme: ' + names[t] + ' — click to cycle');
      orb.setAttribute('title', 'Theme: ' + names[t] + ' — click to cycle');
    }
  }
  function setScanlines(on) {
    document.documentElement.setAttribute('data-scanlines', on ? 'on' : 'off');
    store('jx-scanlines', on ? 'on' : 'off');
  }
  function setGlow(mult) {
    document.documentElement.style.setProperty('--glow-mult', String(mult));
  }

  /* ---------- countdown utils ---------- */
  function daysUntil(dateStr) {
    var target = new Date(dateStr + 'T00:00:00');
    var now = new Date();
    var ms = target - now;
    return {
      ms: ms,
      past: ms < 0,
      days: Math.ceil(ms / 86400000),
      d: Math.floor(Math.abs(ms) / 86400000),
      h: Math.floor((Math.abs(ms) % 86400000) / 3600000),
      m: Math.floor((Math.abs(ms) % 3600000) / 60000),
      s: Math.floor((Math.abs(ms) % 60000) / 1000)
    };
  }

  /* ---------- site-wide search ---------- */
  var PAGE_KEYWORDS = {
    home: 'landing hero start',
    about: 'saga bio cover letter character sheet infj',
    cv: 'resume record of deeds experience skills print pdf',
    portfolio: 'forge projects work case study open source',
    blog: 'codex writing posts articles',
    docs: 'grimoire guides cheat sheets codes walkthroughs patterns reference',
    games: 'game hall tracker playing played favorites upcoming backlog',
    wallpapers: 'hoard stash gallery downloads art',
    countdowns: 'reckoning timers count up down days'
  };

  var searchEls = null; // { modal, input, results }
  var docsIndex = null;
  var selIdx = -1;

  function stripTags(html) {
    var d = document.createElement('div');
    d.innerHTML = html;
    return (d.textContent || '').replace(/\s+/g, ' ');
  }

  function ensureDocs(cb) {
    if (window.JX_DOCS) { cb(); return; }
    // The grimoire lives in the content collection now: the /docs page injects
    // window.JX_DOCS directly; every other page lazy-fetches it for search.
    fetch('/assets/docs-data.json')
      .then(function (r) { return r.json(); })
      .then(function (data) { window.JX_DOCS = data; cb(); })
      .catch(function () { window.JX_DOCS = []; cb(); });
  }

  function buildIndex() {
    if (docsIndex) return docsIndex;
    docsIndex = (window.JX_DOCS || []).map(function (d) {
      return { d: d, text: stripTags(d.body).toLowerCase(), hay: (d.title + ' ' + d.cat + ' ' + (d.game || '') + ' ' + d.tags.join(' ')).toLowerCase() };
    });
    return docsIndex;
  }

  function escHTML(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  function snippet(text, q) {
    var i = text.toLowerCase().indexOf(q);
    if (i === -1) return escHTML(text.slice(0, 90));
    var start = Math.max(0, i - 34);
    var pre = (start > 0 ? '…' : '') + text.slice(start, i);
    var hit = text.slice(i, i + q.length);
    var post = text.slice(i + q.length, i + q.length + 56) + '…';
    return escHTML(pre) + '<mark>' + escHTML(hit) + '</mark>' + escHTML(post);
  }

  function renderResults(qRaw) {
    var q = qRaw.trim().toLowerCase();
    var html = '';
    selIdx = -1;

    var pages = PAGES.filter(function (p) {
      if (!q) return false;
      return (p.label + ' ' + (PAGE_KEYWORDS[p.id] || '')).toLowerCase().indexOf(q) !== -1;
    });
    var docs = q ? buildIndex().filter(function (e) {
      return e.hay.indexOf(q) !== -1 || e.text.indexOf(q) !== -1;
    }) : [];

    if (!q) {
      html = '<div class="sm-group">Pages</div>' + PAGES.map(function (p) {
        return '<a class="sm-item" href="' + p.href + '"><span class="t-row"><span class="t">' + p.label + '</span></span></a>';
      }).join('');
    } else if (!pages.length && !docs.length) {
      html = '<div class="sm-empty">Nothing found. The grimoire keeps its secrets.</div>';
    } else {
      if (pages.length) {
        html += '<div class="sm-group">Pages</div>' + pages.map(function (p) {
          return '<a class="sm-item" href="' + p.href + '"><span class="t-row"><span class="t">' + p.label + '</span></span></a>';
        }).join('');
      }
      if (docs.length) {
        html += '<div class="sm-group">Grimoire</div>' + docs.slice(0, 12).map(function (e) {
          return '<a class="sm-item" href="/docs?doc=' + e.d.id + '">' +
            '<span class="t-row"><span class="t">' + e.d.title + '</span><span class="where">' + e.d.cat + '</span></span>' +
            '<span class="snip">' + snippet(stripTags(e.d.body), q) + '</span>' +
          '</a>';
        }).join('');
      }
    }
    searchEls.results.innerHTML = html;
  }

  function moveSel(dir) {
    var items = searchEls.results.querySelectorAll('.sm-item');
    if (!items.length) return;
    selIdx = (selIdx + dir + items.length) % items.length;
    items.forEach(function (el, i) { el.classList.toggle('sel', i === selIdx); });
    items[selIdx].scrollIntoViewIfNeeded ? items[selIdx].scrollIntoViewIfNeeded(false) : null;
  }

  function openSearch() {
    if (!searchEls) return;
    searchEls.modal.classList.add('open');
    searchEls.input.value = '';
    ensureDocs(function () { renderResults(''); });
    setTimeout(function () { searchEls.input.focus(); }, 30);
  }
  function closeSearch() {
    if (searchEls) searchEls.modal.classList.remove('open');
  }

  function setupSearch() {
    var modal = document.createElement('div');
    modal.className = 'search-modal';
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    modal.setAttribute('aria-label', 'Site search');
    modal.innerHTML =
      '<div class="sm-panel">' +
        '<div class="sm-input">' + ICONS.search.replace('<svg ', '<svg width="16" height="16" ') +
          '<input type="search" placeholder="Search pages &amp; the grimoire…" aria-label="Search">' +
          '<span class="sm-esc">esc</span>' +
        '</div>' +
        '<div class="sm-results"></div>' +
      '</div>';
    document.body.appendChild(modal);

    searchEls = {
      modal: modal,
      input: modal.querySelector('input'),
      results: modal.querySelector('.sm-results')
    };

    modal.addEventListener('click', function (e) { if (e.target === modal) closeSearch(); });
    searchEls.input.addEventListener('input', function () {
      ensureDocs(function () { renderResults(searchEls.input.value); });
    });
    searchEls.input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); moveSel(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); moveSel(-1); }
      else if (e.key === 'Enter') {
        var items = searchEls.results.querySelectorAll('.sm-item');
        var pick = items[selIdx === -1 ? 0 : selIdx];
        if (pick) window.location.href = pick.getAttribute('href');
      }
    });

    document.addEventListener('keydown', function (e) {
      var tag = (e.target.tagName || '').toLowerCase();
      var typing = tag === 'input' || tag === 'textarea' || e.target.isContentEditable;
      if (e.key === 'Escape') { closeSearch(); return; }
      if (typing) return;
      if (e.key === '/' || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k')) {
        e.preventDefault();
        openSearch();
      }
    });

    document.querySelectorAll('.nav-search').forEach(function (b) {
      b.addEventListener('click', openSearch);
    });
  }

  /* ---------- init ---------- */
  function init() {
    var body = document.body;

    // theme
    setTheme(read('jx-theme') || getTheme());
    if (read('jx-scanlines') === 'off') setScanlines(false);
    document.querySelectorAll('.theme-toggle button').forEach(function (b) {
      b.addEventListener('click', function () { setTheme(b.getAttribute('data-set')); });
    });

    // lang — buttons carry server-computed per-locale URLs; remember the choice and navigate
    document.querySelectorAll('.lang-toggle button').forEach(function (b) {
      b.addEventListener('click', function () {
        var lang = b.getAttribute('data-lang');
        var href = b.getAttribute('data-href');
        if (lang) store('jx-lang', lang);
        if (href) window.location.href = href;
      });
    });

    // adaptive nav behaviors
    var orb = document.querySelector('.nav-orb');
    if (orb) {
      orb.addEventListener('click', function () {
        setTheme(THEMES[(THEMES.indexOf(getTheme()) + 1) % THEMES.length]);
      });
      setTheme(getTheme()); // paint orb icon
    }

    function closeRealms(except) {
      document.querySelectorAll('.nav-realm').forEach(function (r) {
        if (r === except) return;
        r.querySelector('.nav-rpanel').hidden = true;
        r.querySelector('.nav-rtop').classList.remove('open');
        r.querySelector('.nav-rtop').setAttribute('aria-expanded', 'false');
      });
    }
    document.querySelectorAll('.nav-rtop').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var realm = btn.parentElement;
        var panel = realm.querySelector('.nav-rpanel');
        var opening = panel.hidden;
        closeRealms(opening ? realm : null);
        panel.hidden = !opening;
        btn.classList.toggle('open', opening);
        btn.setAttribute('aria-expanded', String(opening));
      });
    });

    var menubtn = document.querySelector('.nav-menubtn');
    var mega = document.querySelector('.nav-mega');
    if (menubtn && mega) {
      menubtn.addEventListener('click', function () {
        var opening = mega.hidden;
        mega.hidden = !opening;
        menubtn.classList.toggle('open', opening);
        menubtn.setAttribute('aria-expanded', String(opening));
      });
    }

    var seek = document.querySelector('.nav-seek');
    if (seek) seek.addEventListener('click', openSearch);

    var moreBtn = document.querySelector('.jx-more');
    var sheet = document.querySelector('.jx-sheet');
    if (moreBtn && sheet) {
      moreBtn.addEventListener('click', function () {
        var opening = sheet.hidden;
        sheet.hidden = !opening;
        moreBtn.setAttribute('aria-expanded', String(opening));
      });
    }

    document.addEventListener('click', function (e) {
      if (!e.target.closest('.nav-realm')) closeRealms(null);
      if (mega && !mega.hidden && !e.target.closest('.nav-mega') && !e.target.closest('.nav-menubtn')) {
        mega.hidden = true;
        menubtn.classList.remove('open');
        menubtn.setAttribute('aria-expanded', 'false');
      }
      if (sheet && !sheet.hidden && !e.target.closest('.jx-sheet') && !e.target.closest('.jx-more')) {
        sheet.hidden = true;
        moreBtn.setAttribute('aria-expanded', 'false');
      }
    });
    document.addEventListener('keydown', function (e) {
      if (e.key !== 'Escape') return;
      closeRealms(null);
      if (mega && !mega.hidden) { mega.hidden = true; menubtn.classList.remove('open'); menubtn.setAttribute('aria-expanded', 'false'); }
      if (sheet && !sheet.hidden) { sheet.hidden = true; moreBtn.setAttribute('aria-expanded', 'false'); }
    });

    // site-wide search
    setupSearch();

    // reveal on scroll
    if ('IntersectionObserver' in window) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
        });
      }, { threshold: 0.12 });
      document.querySelectorAll('.reveal').forEach(function (el) { io.observe(el); });
    } else {
      document.querySelectorAll('.reveal').forEach(function (el) { el.classList.add('in'); });
    }

    // dragonfly easter egg (pages that opt in)
    if (body.getAttribute('data-dragonfly') === 'on') {
      setTimeout(function () {
        var d = document.createElement('div');
        d.className = 'dragonfly';
        d.innerHTML = ICONS.dfly;
        body.appendChild(d);
        setTimeout(function () { d.remove(); }, 15000);
      }, 6000);
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  window.JX = {
    setTheme: setTheme,
    getTheme: getTheme,
    setScanlines: setScanlines,
    setGlow: setGlow,
    daysUntil: daysUntil,
    openSearch: openSearch,
    icons: ICONS,
    pages: PAGES
  };
})();
