// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.SiteBuild;

/// <summary>
/// The frame every page is poured into: head, header, footer.
/// </summary>
/// <remarks>
/// One shell, so the navigation and the theme toggle cannot drift between a
/// hand-written page and a rendered one. Pages supply a body; everything
/// around it is here.
/// </remarks>
internal static class Shell
{
	/// <summary>Where the site lives, for canonical URLs and social cards.</summary>
	private const string BaseUrl = "https://nickprotop.github.io/ConsoleEx";

	/// <summary>Wraps a body in the site frame.</summary>
	/// <param name="title">The page title, without the site name.</param>
	/// <param name="description">One sentence, for search results and link previews.</param>
	/// <param name="body">The page's own markup.</param>
	/// <param name="active">Which nav item to mark, or null for none.</param>
	/// <param name="depth">How many directories deep the page sits, for relative links.</param>
	internal static string Render(string title, string description, string body, string? active, int depth)
	{
		string up = depth == 0 ? "" : string.Concat(Enumerable.Repeat("../", depth));

		var sb = new StringBuilder();

		sb.AppendLine("<!DOCTYPE html>");
		sb.AppendLine("<html lang=\"en\">");
		sb.AppendLine("<head>");
		sb.AppendLine("<meta charset=\"UTF-8\">");
		sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
		sb.AppendLine($"<title>{Esc(title)} — SharpConsoleUI</title>");
		sb.AppendLine($"<meta name=\"description\" content=\"{Esc(description)}\">");
		sb.AppendLine($"<meta property=\"og:title\" content=\"{Esc(title)} — SharpConsoleUI\">");
		sb.AppendLine($"<meta property=\"og:description\" content=\"{Esc(description)}\">");
		sb.AppendLine($"<meta property=\"og:image\" content=\"{BaseUrl}/images/showcase-full.png\">");
		sb.AppendLine("<meta name=\"twitter:card\" content=\"summary_large_image\">");
		sb.AppendLine("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
		sb.AppendLine("<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
		sb.AppendLine("<link href=\"https://fonts.googleapis.com/css2?family=Fragment+Mono:ital@0;1&family=DM+Sans:wght@300;400;500;600&family=Fira+Code:wght@400;500&display=swap\" rel=\"stylesheet\">");
		sb.AppendLine($"<link rel=\"icon\" href=\"{up}images/logo.svg\">");
		sb.AppendLine($"<link rel=\"stylesheet\" href=\"{up}styles/site.css\">");

		// Before first paint, so a reader who chose light never sees a dark
		// flash on the way in. Inline and synchronous for the same reason.
		sb.AppendLine("<script>(function(){try{var t=localStorage.getItem('scui-theme');"
			+ "if(t)document.documentElement.setAttribute('data-theme',t);}catch(e){}})();</script>");

		// Code blocks. Markdig already tags them "language-csharp" and friends;
		// this is what colours them. Only the languages this documentation
		// actually uses, so the page does not fetch a megabyte of grammars.
		// Both themes, each behind a media query, so the one that applies is the
		// one that matches. A single dark stylesheet put light-grey syntax
		// colours on the light theme's near-white code background, which left
		// commands legible only where they happened not to be highlighted.
		//
		// The media attribute picks by system preference; the data-theme toggle
		// can disagree with it, so the script below follows the toggle too.
		sb.AppendLine("<link rel=\"stylesheet\" id=\"hljs-light\" media=\"(prefers-color-scheme: light)\" "
			+ "href=\"https://cdnjs.cloudflare.com/ajax/libs/"
			+ "highlight.js/11.9.0/styles/github.min.css\">");
		sb.AppendLine("<link rel=\"stylesheet\" id=\"hljs-dark\" media=\"(prefers-color-scheme: dark)\" "
			+ "href=\"https://cdnjs.cloudflare.com/ajax/libs/"
			+ "highlight.js/11.9.0/styles/github-dark.min.css\">");
		sb.AppendLine("<script src=\"https://cdnjs.cloudflare.com/ajax/libs/"
			+ "highlight.js/11.9.0/highlight.min.js\"></script>");

		foreach (string lang in new[] { "csharp", "bash", "json", "xml", "yaml" })
		{
			sb.AppendLine($"<script src=\"https://cdnjs.cloudflare.com/ajax/libs/"
				+ $"highlight.js/11.9.0/languages/{lang}.min.js\"></script>");
		}

		sb.AppendLine("</head>");
		sb.AppendLine("<body>");

		Header(sb, up, active);

		// The ambient wash, on every page that does not already have the home
		// page's stronger one. Emitted here rather than in each page so a page
		// added later gets it without remembering to.
		if (!body.Contains("hero-wash", StringComparison.Ordinal))
			sb.AppendLine("<div class=\"page-wash\"></div>");

		sb.AppendLine(body);

		Footer(sb, up);

		// The toggle writes the choice down; the inline script above reads it.
		sb.AppendLine("<script>");
		sb.AppendLine("(function(){");

		// The two highlight.js stylesheets are chosen by media query, which
		// follows the SYSTEM preference. The toggle can disagree with it, so the
		// media attribute is rewritten to match whichever theme is actually on -
		// otherwise someone on a dark desktop who picks the light theme reads
		// pale syntax colours on a near-white background.
		sb.AppendLine("  function syntax(theme){");
		sb.AppendLine("    var l=document.getElementById('hljs-light');");
		sb.AppendLine("    var d=document.getElementById('hljs-dark');");
		sb.AppendLine("    if(!l||!d)return;");
		sb.AppendLine("    if(theme){l.media=theme==='light'?'all':'not all';");
		sb.AppendLine("              d.media=theme==='dark'?'all':'not all';}");
		sb.AppendLine("    else{l.media='(prefers-color-scheme: light)';");
		sb.AppendLine("         d.media='(prefers-color-scheme: dark)';}");
		sb.AppendLine("  }");
		sb.AppendLine("  try{syntax(document.documentElement.getAttribute('data-theme'));}catch(e){}");

		// Scrollspy for the "on this page" list. The list is plain links and
		// works without this; what it adds is knowing WHERE you are, which on a
		// reference page several screens long is the thing a map is for.
		sb.AppendLine("  (function(){");
		sb.AppendLine("    var toc=document.querySelector('.doc-toc');if(!toc)return;");
		sb.AppendLine("    var links={},ids=[];");
		sb.AppendLine("    toc.querySelectorAll('a[href^=\"#\"]').forEach(function(a){");
		sb.AppendLine("      var id=decodeURIComponent(a.getAttribute('href').slice(1));");
		sb.AppendLine("      if(document.getElementById(id)){links[id]=a;ids.push(id);}");
		sb.AppendLine("    });");
		sb.AppendLine("    if(!ids.length)return;");
		sb.AppendLine("    var here=null;");
		sb.AppendLine("    function mark(id){");
		sb.AppendLine("      if(id===here)return;");
		sb.AppendLine("      if(here&&links[here])links[here].classList.remove('here');");
		sb.AppendLine("      here=id;");
		sb.AppendLine("      if(links[id]){links[id].classList.add('here');");
		sb.AppendLine("        var r=links[id].getBoundingClientRect(),b=toc.getBoundingClientRect();");
		sb.AppendLine("        if(r.top<b.top||r.bottom>b.bottom)links[id].scrollIntoView({block:'nearest'});}");
		sb.AppendLine("    }");

		// Chosen by position rather than by which heading the observer fired
		// for: several can cross the line in one scroll, and the last to fire
		// is not the one at the top of the screen.
		sb.AppendLine("    function current(){");
		sb.AppendLine("      var best=ids[0],top=120;");
		sb.AppendLine("      for(var i=0;i<ids.length;i++){");
		sb.AppendLine("        var el=document.getElementById(ids[i]);if(!el)continue;");
		sb.AppendLine("        if(el.getBoundingClientRect().top<=top)best=ids[i];else break;");
		sb.AppendLine("      }");
		sb.AppendLine("      return best;");
		sb.AppendLine("    }");

		// Read on a frame rather than on every scroll event, so a fast scroll
		// does not queue a layout read per pixel.
		sb.AppendLine("    var queued=false;");
		sb.AppendLine("    function tick(){queued=false;mark(current());}");
		sb.AppendLine("    window.addEventListener('scroll',function(){");
		sb.AppendLine("      if(queued)return;queued=true;requestAnimationFrame(tick);");
		sb.AppendLine("    },{passive:true});");
		sb.AppendLine("    mark(current());");
		sb.AppendLine("  })();");

		// Copy buttons. The point of this site is getting a command into
		// someone's terminal, and selecting monospace text that wraps is the
		// step where that goes wrong. Added here rather than in the generator
		// so every code block gets one, including the ones in rendered
		// markdown that the generator never sees individually.
		sb.AppendLine("  function copyable(el,text,label){");
		sb.AppendLine("    if(el.querySelector('.copy'))return;");
		sb.AppendLine("    var btn=document.createElement('button');");
		sb.AppendLine("    btn.className='copy';btn.type='button';");
		sb.AppendLine("    btn.setAttribute('aria-label','Copy to clipboard');");
		sb.AppendLine("    btn.textContent='Copy';");
		sb.AppendLine("    btn.addEventListener('click',function(){");
		sb.AppendLine("      navigator.clipboard.writeText(text()).then(function(){");
		sb.AppendLine("        btn.textContent='Copied';btn.classList.add('ok');");
		sb.AppendLine("        setTimeout(function(){btn.textContent='Copy';btn.classList.remove('ok');},1400);");
		sb.AppendLine("      }).catch(function(){btn.textContent='Press Ctrl+C';");
		sb.AppendLine("        setTimeout(function(){btn.textContent='Copy';},1800);});");
		sb.AppendLine("    });");
		sb.AppendLine("    el.appendChild(btn);");
		sb.AppendLine("    if(label){var t=document.createElement('span');");
		sb.AppendLine("      t.className='lang';t.textContent=label;el.appendChild(t);}");
		sb.AppendLine("  }");
		sb.AppendLine("  document.querySelectorAll('.prose pre').forEach(function(pre){");
		sb.AppendLine("    var code=pre.querySelector('code');if(!code)return;");
		sb.AppendLine("    var m=(code.className||'').match(/language-([a-z0-9]+)/i);");
		sb.AppendLine("    copyable(pre,function(){return code.innerText;},m?m[1]:'');");
		sb.AppendLine("  });");
		sb.AppendLine("  document.querySelectorAll('.hero-install').forEach(function(p){");
		sb.AppendLine("    var code=p.querySelector('code');if(!code)return;");
		sb.AppendLine("    copyable(p,function(){return code.innerText;},'');");
		sb.AppendLine("  });");

		sb.AppendLine("  var b=document.getElementById('themeToggle');");
		sb.AppendLine("  if(!b)return;");
		sb.AppendLine("  b.addEventListener('click',function(){");
		sb.AppendLine("    var r=document.documentElement;");
		sb.AppendLine("    var cur=r.getAttribute('data-theme');");
		sb.AppendLine("    if(!cur)cur=window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light';");
		sb.AppendLine("    var next=cur==='dark'?'light':'dark';");
		sb.AppendLine("    r.setAttribute('data-theme',next);");
		sb.AppendLine("    syntax(next);");
		sb.AppendLine("    try{localStorage.setItem('scui-theme',next);}catch(e){}");
		sb.AppendLine("  });");
		sb.AppendLine("})();");

		// Reveal on scroll, and the hero carousel. Both no-op on a page that
		// has neither, so one script serves every page.
		sb.AppendLine("(function(){");
		sb.AppendLine("  var els=document.querySelectorAll('.reveal');");
		sb.AppendLine("  if(els.length){");
		sb.AppendLine("    if(!('IntersectionObserver' in window)||window.matchMedia('(prefers-reduced-motion: reduce)').matches){");
		sb.AppendLine("      els.forEach(function(e){e.classList.add('in');});");
		sb.AppendLine("    }else{");
		sb.AppendLine("      var io=new IntersectionObserver(function(es){");
		sb.AppendLine("        es.forEach(function(e){if(e.isIntersecting){e.target.classList.add('in');io.unobserve(e.target);}});");
		sb.AppendLine("      },{rootMargin:'0px 0px -60px 0px'});");
		sb.AppendLine("      els.forEach(function(e){io.observe(e);});");
		sb.AppendLine("    }");
		sb.AppendLine("  }");
		sb.AppendLine("  var car=document.getElementById('heroCarousel');");
		sb.AppendLine("  if(!car)return;");
		sb.AppendLine("  var slides=car.querySelectorAll('.carousel-slide');");
		sb.AppendLine("  var dots=document.querySelectorAll('.carousel-dot');");
		sb.AppendLine("  var at=0,timer=null;");
		sb.AppendLine("  function go(i){");
		sb.AppendLine("    at=(i+slides.length)%slides.length;");
		sb.AppendLine("    slides.forEach(function(s,n){s.classList.toggle('active',n===at);});");
		sb.AppendLine("    dots.forEach(function(d,n){d.classList.toggle('active',n===at);});");
		sb.AppendLine("  }");
		sb.AppendLine("  function play(){");
		sb.AppendLine("    if(window.matchMedia('(prefers-reduced-motion: reduce)').matches)return;");
		sb.AppendLine("    stop();");
		sb.AppendLine("    var hold=function(){return at===0?9000:5200;};");
		sb.AppendLine("    var tick=function(){go(at+1);timer=setTimeout(tick,hold());};");
		sb.AppendLine("    timer=setTimeout(tick,hold());");
		sb.AppendLine("  }");
		sb.AppendLine("  function stop(){if(timer){clearTimeout(timer);timer=null;}}");
		sb.AppendLine("  dots.forEach(function(d){d.addEventListener('click',function(){go(+d.dataset.index);play();});});");
		sb.AppendLine("  var prev=car.parentNode.querySelector('.carousel-nav.prev');");
		sb.AppendLine("  var next=car.parentNode.querySelector('.carousel-nav.next');");
		sb.AppendLine("  if(prev)prev.addEventListener('click',function(){go(at-1);play();});");
		sb.AppendLine("  if(next)next.addEventListener('click',function(){go(at+1);play();});");
		sb.AppendLine("  car.addEventListener('mouseenter',stop);");
		sb.AppendLine("  car.addEventListener('mouseleave',play);");
		sb.AppendLine("  document.addEventListener('visibilitychange',function(){document.hidden?stop():play();});");
		sb.AppendLine("  play();");
		sb.AppendLine("})();");

		// Showcase thumbnails replace the hero beside them.
		sb.AppendLine("(function(){");
		sb.AppendLine("  document.querySelectorAll('.show-media').forEach(function(m){");
		sb.AppendLine("    var hero=m.querySelector('.show-hero');");
		sb.AppendLine("    var thumbs=m.querySelectorAll('.show-thumb');");
		sb.AppendLine("    if(!hero||!thumbs.length)return;");
		sb.AppendLine("    thumbs.forEach(function(t){");
		sb.AppendLine("      t.addEventListener('click',function(){");
		sb.AppendLine("        var src=t.dataset.src;");
		sb.AppendLine("        if(!src||hero.getAttribute('src')===src)return;");
		sb.AppendLine("        hero.style.opacity='0';");
		sb.AppendLine("        var done=function(){hero.setAttribute('src',src);hero.style.opacity='';};");
		sb.AppendLine("        if(window.matchMedia('(prefers-reduced-motion: reduce)').matches)done();");
		sb.AppendLine("        else setTimeout(done,140);");
		sb.AppendLine("        thumbs.forEach(function(o){o.classList.toggle('is-on',o===t);});");
		sb.AppendLine("      });");
		sb.AppendLine("    });");
		sb.AppendLine("  });");
		sb.AppendLine("})();");
		sb.AppendLine("</script>");

		// Highlight only what Markdig labelled; an unlabelled block stays plain
		// rather than being guessed at, which guesses wrong on short snippets.
		sb.AppendLine("<script>");
		sb.AppendLine("(function(){");
		sb.AppendLine("  if(!window.hljs)return;");
		sb.AppendLine("  document.querySelectorAll('pre code[class*=\"language-\"]')");
		sb.AppendLine("    .forEach(function(b){try{hljs.highlightElement(b);}catch(e){}});");
		sb.AppendLine("})();");
		sb.AppendLine("</script>");

		sb.AppendLine("</body>");
		sb.AppendLine("</html>");

		return sb.ToString();
	}

	private static void Header(StringBuilder sb, string up, string? active)
	{
		string A(string id) => active == id ? " class=\"active\"" : "";

		sb.AppendLine("<header class=\"site-header\"><div class=\"site-header-inner\">");
		sb.AppendLine($"<a class=\"site-brand\" href=\"{up}index.html\">"
			+ "<span class=\"mark\">▎</span><span class=\"full\">SharpConsoleUI</span></a>");
		sb.AppendLine("<nav class=\"site-nav\">");
		sb.AppendLine($"<a href=\"{up}controls.html\"{A("controls")}>Controls</a>");
		sb.AppendLine($"<a href=\"{up}showcase.html\"{A("showcase")}>Showcase</a>");
		sb.AppendLine($"<a href=\"{up}docs/index.html\"{A("docs")}>Docs</a>");
		sb.AppendLine($"<a href=\"{up}api/index.html\">API</a>");
		sb.AppendLine("<a href=\"https://github.com/nickprotop/ConsoleEx\">GitHub</a>");
		sb.AppendLine("<button class=\"theme-toggle\" id=\"themeToggle\" type=\"button\" "
			+ "aria-label=\"Switch between light and dark\" title=\"Switch between light and dark\">");
		sb.AppendLine(SunIcon());
		sb.AppendLine(MoonIcon());
		sb.AppendLine("</button>");
		sb.AppendLine("</nav>");
		sb.AppendLine("</div></header>");
	}

	private static void Footer(StringBuilder sb, string up)
	{
		sb.AppendLine("<footer class=\"site-footer\"><div class=\"site-footer-inner\">");
		sb.AppendLine("<span>MIT licensed</span>");
		sb.AppendLine($"<a href=\"{up}docs/index.html\">Documentation</a>");
		sb.AppendLine("<a href=\"https://www.nuget.org/packages/SharpConsoleUI\">NuGet</a>");
		sb.AppendLine("<a href=\"https://github.com/nickprotop/ConsoleEx\">GitHub</a>");
		sb.AppendLine("<span class=\"sep\">Built for terminals that deserve better.</span>");
		sb.AppendLine("</div></footer>");
	}

	private static string SunIcon() =>
		"<svg class=\"i-sun\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" "
		+ "stroke-width=\"2\" stroke-linecap=\"round\"><circle cx=\"12\" cy=\"12\" r=\"4.2\"/>"
		+ "<path d=\"M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2"
		+ "M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4\"/></svg>";

	private static string MoonIcon() =>
		"<svg class=\"i-moon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" "
		+ "stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">"
		+ "<path d=\"M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z\"/></svg>";

	/// <summary>Text that cannot be read as markup.</summary>
	internal static string Esc(string s) =>
		s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
