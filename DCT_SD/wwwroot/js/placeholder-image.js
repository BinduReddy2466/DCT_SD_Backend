// Port of placeholderImageUrl (React frontend/src/utils/placeholderImage.ts). There is no
// document-image storage/serving in this backend yet, so the viewer renders a locally
// generated inline SVG data URI keyed off the filename - no network call, always renders.
window.dctPlaceholderImage = function (label, size) {
  size = size || '450x600';
  var parts = size.split('x').map(Number);
  var width = parts[0];
  var height = parts[1];
  var escaped = String(label || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  var fontSize = Math.max(14, Math.round(width / 14));

  var svg =
    '<svg xmlns="http://www.w3.org/2000/svg" width="' + width + '" height="' + height + '" viewBox="0 0 ' + width + ' ' + height + '">' +
    '<rect width="100%" height="100%" fill="#F8FAFC"/>' +
    '<text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" font-family="system-ui, sans-serif" font-size="' + fontSize + '" fill="#475569">' + escaped + '</text>' +
    '</svg>';

  return 'data:image/svg+xml;charset=UTF-8,' + encodeURIComponent(svg);
};
