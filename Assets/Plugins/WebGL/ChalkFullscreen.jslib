// Full screen for the web build. The page itself goes full screen (its canvas already fills the page), so the game
// covers the whole screen at its own resolution. A site's full-screen button only centres the embed at its page size,
// which leaves black bars around it.
// Browsers allow full screen only inside a user gesture, and Unity sees a click a frame or more later (a quick tap is
// over by then). So the game tells the page where its button is, and the page itself toggles full screen when a press
// is released over that spot.
mergeInto(LibraryManager.library, {
  ChalkFullscreenInit: function () {
    if (window.chalkFs) return;
    var s = window.chalkFs = { x0: 0, y0: 0, x1: -1, y1: -1 };
    window.addEventListener('pointerup', function (e) {
      var c = Module['canvas'];
      if (!c) return;
      var r = c.getBoundingClientRect();
      if (r.width <= 0 || r.height <= 0) return;
      var u = (e.clientX - r.left) / r.width, v = (e.clientY - r.top) / r.height;
      if (u < s.x0 || u > s.x1 || v < s.y0 || v > s.y1) return;
      var d = document, el = d.documentElement, p = null;
      try {
        if (d.fullscreenElement || d.webkitFullscreenElement) p = (d.exitFullscreen || d.webkitExitFullscreen).call(d);
        else p = (el.requestFullscreen || el.webkitRequestFullscreen).call(el);
      } catch (x) {}
      if (p && p.catch) p.catch(function () {});
    }, true);
  },
  // the button's box as fractions of the canvas, y down; an empty box (x1 < x0) switches the spot off
  ChalkFullscreenArea: function (x0, y0, x1, y1) {
    var s = window.chalkFs;
    if (s) { s.x0 = x0; s.y0 = y0; s.x1 = x1; s.y1 = y1; }
  },
  ChalkFullscreenIs: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  }
});
