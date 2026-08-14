window.rsyDownloadFile = function (fileName, contentType, base64) {
  const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
  const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName || 'download';
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
};
