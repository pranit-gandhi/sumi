"""Send a command to Sumi's project-local Editor automation mailbox."""
import pathlib,sys,time
root=pathlib.Path(__file__).resolve().parents[1]
request=root/'Tools/editor-command.txt';response=root/'Logs/editor-result.txt'
before=response.stat().st_mtime_ns if response.exists() else 0
request.write_text(sys.argv[1],encoding='utf-8')
deadline=time.monotonic()+55
while time.monotonic()<deadline:
    if not request.exists() and response.exists() and response.stat().st_mtime_ns>before:
        print(response.read_text(encoding='utf-8-sig'));break
    time.sleep(.25)
else:sys.exit('Editor command pending; inspect Logs/Editor.log')
