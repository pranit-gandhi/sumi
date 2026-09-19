"""Instantiate installed Unity URP template without copying unrelated projects."""
import json, pathlib, tarfile
root = pathlib.Path(__file__).resolve().parents[1]
archive = pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.2.14f1\Editor\Data\Resources\PackageManager\ProjectTemplates\com.unity.template.3d-cross-platform-17.0.14.tgz')
assert not (root / 'Assets').exists(), 'Never overwrite an existing project'
prefix = 'package/ProjectData~/'
with tarfile.open(archive) as tar:
    for member in tar:
        if not member.isfile() or not member.name.startswith(prefix): continue
        relative = pathlib.PurePosixPath(member.name[len(prefix):])
        if relative.parts[0] not in ('Assets','Packages','ProjectSettings'): continue
        dest = root.joinpath(*relative.parts).resolve()
        assert dest.is_relative_to(root)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(tar.extractfile(member).read())
manifest = root / 'Packages/manifest.json'
data = json.loads(manifest.read_text())
data['dependencies'] = {
 'com.unity.ai.assistant': '2.19.0-pre.2',
 'com.unity.cinemachine': '3.1.2',
 'com.unity.inputsystem': '1.16.0',
 'com.unity.render-pipelines.universal': '17.2.0',
 'com.unity.test-framework': '1.4.6',
 'com.unity.ugui': '2.0.0',
 'com.unity.modules.audio': '1.0.0',
 'com.unity.modules.animation': '1.0.0',
 'com.unity.modules.physics': '1.0.0',
 'com.unity.modules.particlesystem': '1.0.0',
 'com.unity.modules.imgui': '1.0.0',
 'com.unity.modules.imageconversion': '1.0.0',
 'com.unity.modules.jsonserialize': '1.0.0',
 'com.unity.modules.screencapture': '1.0.0',
 'com.unity.modules.ui': '1.0.0',
 'com.unity.modules.uielements': '1.0.0',
 'com.unity.modules.unitywebrequest': '1.0.0',
}
manifest.write_text(json.dumps(data, indent=2)+'\n')
(root/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.2.14f1\nm_EditorVersionWithRevision: 6000.2.14f1 (589824c1fc31)\n')
print('Created Unity URP project at', root)
