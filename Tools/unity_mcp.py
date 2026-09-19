"""Call the installed Unity MCP relay, explicitly bound to this project."""
import json, pathlib, subprocess, sys, threading, queue, time, base64
ROOT = pathlib.Path(__file__).resolve().parents[1]
def main():
    q = queue.Queue()
    log = (ROOT/'Logs/relay.log').open('a', encoding='utf-8')
    p = subprocess.Popen([str(pathlib.Path.home()/'.unity/relay/relay_win.exe'), '--mcp', '--project-path', str(ROOT)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf-8', creationflags=0x08000000)
    def read():
        for line in p.stdout:
            try: q.put(json.loads(line))
            except ValueError: pass
    threading.Thread(target=read,daemon=True).start()
    def request(i, method, params):
        p.stdin.write(json.dumps(dict(jsonrpc='2.0',id=i,method=method,params=params))+'\n');p.stdin.flush()
        deadline=time.monotonic()+150
        while time.monotonic()<deadline:
            try: response=q.get(timeout=1)
            except queue.Empty: continue
            if response.get('id')==i: return response
        raise TimeoutError(method)
    try:
        print(json.dumps(request(1,'initialize',dict(protocolVersion='2024-11-05',capabilities={},clientInfo=dict(name='Sumi-Editor-Automation',version='1.0')))))
        p.stdin.write(json.dumps(dict(jsonrpc='2.0',method='notifications/initialized'))+'\n');p.stdin.flush()
        if len(sys.argv)<2: response=request(2,'tools/list',{})
        else:
            params=json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig')) if len(sys.argv)>2 else {}
            response=request(2,'tools/call',dict(name=sys.argv[1],arguments=params))
        for block in response.get('result',{}).get('content',[]):
            if block.get('type')=='image':
                path=ROOT/'Logs/mcp-capture.png';path.write_bytes(base64.b64decode(block.pop('data')));block['saved']=str(path)
        print(json.dumps(response,ensure_ascii=True))
    finally: p.terminate();log.close()
if __name__=='__main__': main()
