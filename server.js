const http = require('http');
function sseFrame(data) { return `data: ${JSON.stringify(data)}\n\n`; }
const server = http.createServer((req, res) => {
  if (req.method === 'POST' && req.url === '/fetch/start') {
    req.socket.setNoDelay(true); res.socket.setNoDelay(true);
    res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', 'Connection': 'keep-alive' });
    res.flushHeaders();
    const steps = [
      { delay: 300, data: { event: 'connectivity_check', database: true, llm: true, root_path: true, ok: true, message: null } },
      { delay: 600, data: { event: 'folder_inventory', total_folders: 5, already_processed: 0, to_process: 5, valid_folders: 5, invalid_folders: 0 } },
      { delay: 800, data: { event: 'folder_result', folder_path: 'D:\RD\021\E1', rd_code: '021', status: 'failed', reason: 'llm unavailable: simulated Bedrock outage on call 8' } },
      { delay: 500, data: { event: 'system_error', system: 'llm', reason: 'simulated Bedrock outage on call 8', message: 'Stopping fetch run - llm became unavailable' } },
      { delay: 300, data: { event: 'run_complete', fetch_run_id: 6002, status: 'Failed', processed_count: 0, total_count: 5 } },
    ];
    let i = 0;
    function next() { if (i >= steps.length) { res.end(); return; } const s = steps[i]; i++; setTimeout(() => { res.write(sseFrame(s.data)); next(); }, s.delay); }
    next();
    return;
  }
  if (req.method === 'GET' && req.url === '/fetch/6002') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ id: 6002, status: 'Failed', processed_count: 0, total_count: 5, run_time_seconds: 1.6, executed_by: 'fake', source_path: 'D:\RD', last_processed_folder_path: null, started_at: new Date(Date.now()-1600).toISOString(), completed_at: new Date().toISOString() }));
    return;
  }
  res.writeHead(404, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ detail: 'Fetch run not found' }));
});
server.listen(5299, () => console.log('Fake fetch API (failure, live row) listening on http://localhost:5299'));
