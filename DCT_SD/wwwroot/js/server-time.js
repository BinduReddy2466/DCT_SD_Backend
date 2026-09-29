// Client-side mirror of Helpers/DisplayTime.cs's ToLocalDisplay() - the one place any page-side
// JS should go through to show a date/time to the user, so client-rendered values stay
// consistent with the server-rendered ones instead of silently falling back to the VIEWER's own
// browser/OS timezone. There is no fixed target timezone (see DisplayTime.cs's own comment) - the
// server embeds its own currently-configured UTC offset on <body data-server-utc-offset-minutes>
// (see _Layout.cshtml), and every value here is shifted by that same offset, so this always
// agrees with whatever the server itself is set to, on any machine, without any timezone name
// needing to be known or hardcoded on either side.
(function () {
  'use strict';

  function getServerOffsetMinutes() {
    var raw = document.body ? document.body.getAttribute('data-server-utc-offset-minutes') : null;
    var parsed = raw === null ? NaN : parseInt(raw, 10);
    return isNaN(parsed) ? 0 : parsed;
  }

  // `value` is a Date, or an ISO-ish string as returned by System.Text.Json for a DateTime with
  // Kind=Unspecified (no trailing "Z") - which System.Text.Json always leaves off since EF Core
  // returns SQL Server datetime2 columns as Kind=Unspecified, even though every such value in
  // this app is actually a UTC instant (see DisplayTime.cs). A bare "Z" is appended when missing
  // so the browser's Date parser interprets it as the correct UTC instant instead of treating it
  // as already being in the browser's own local time.
  function toUtcDate(value) {
    if (value instanceof Date) return value;
    var text = String(value);
    if (!/Z$|[+-]\d{2}:?\d{2}$/.test(text)) {
      text += 'Z';
    }
    return new Date(text);
  }

  // Applies the server's own UTC offset by hand, then reads the result back out using the
  // getUTC*() accessors - reading the *shifted* instant's UTC components, rather than its local
  // ones, is what avoids the browser ALSO applying the viewer's own local offset on top.
  function formatWithServerOffset(date, options) {
    var shifted = new Date(date.getTime() + getServerOffsetMinutes() * 60000);
    var utcAsLocal = new Date(Date.UTC(
      shifted.getUTCFullYear(), shifted.getUTCMonth(), shifted.getUTCDate(),
      shifted.getUTCHours(), shifted.getUTCMinutes(), shifted.getUTCSeconds()
    ));
    return utcAsLocal.toLocaleString('en-US', Object.assign({ timeZone: 'UTC' }, options));
  }

  window.formatServerDateTime = function (value) {
    return formatWithServerOffset(toUtcDate(value), {
      month: '2-digit', day: '2-digit', year: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
    });
  };

  window.formatServerClock = function (date) {
    return formatWithServerOffset(date || new Date(), {
      month: 'short', day: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit', hour12: true,
    });
  };
})();
