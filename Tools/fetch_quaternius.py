import requests,re,pathlib,zipfile
s=requests.Session()
out=pathlib.Path('Tools/AssetSources');out.mkdir(exist_ok=True)
for slug in ['universal-base-characters','universal-animation-library']:
 u='https://quaternius.itch.io/'+slug
 t=s.get(u).text
 token=re.search(r'name="csrf_token" value="([^"]+)',t).group(1)
 dl=s.post(u+'/download_url',data={'csrf_token':token}).json()['url']
 page=s.get(dl).text
 uid=re.search(r'data-upload_id="(\d+)"',page).group(1)
 r=s.post(u+'/file/'+uid,data={'csrf_token':token},headers={'Referer':dl})
 print(slug,r.status_code,r.text[:100],flush=True)
 if r.ok and 'url' in r.json():
  data=s.get(r.json()['url']);data.raise_for_status()
  dest=out/(slug+'.zip');dest.write_bytes(data.content)
  z=zipfile.ZipFile(dest)
  print('Downloaded',len(data.content),'bytes',len(z.namelist()),'files',flush=True)
  print('\n'.join(z.namelist()[:80]),flush=True)
