import zipfile,pathlib
out=pathlib.Path('Assets/Sumi/Characters');out.mkdir(exist_ok=True)
for slug in ['universal-base-characters','universal-animation-library']:
 z=zipfile.ZipFile('Tools/AssetSources/'+slug+'.zip')
 for n in z.namelist():
  if n.endswith('/Unity/Superhero_Male_FullBody.fbx') or n.endswith('/Unity/UAL1_Standard.fbx'):
   (out/pathlib.PurePosixPath(n).name).write_bytes(z.read(n))
  if n.lower().endswith('license.txt'):
   (out/(slug+'-LICENSE.txt')).write_bytes(z.read(n))
 print(slug,[n for n in z.namelist() if 'license' in n.lower()])
