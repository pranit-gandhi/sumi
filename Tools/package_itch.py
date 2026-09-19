"""Package the current Sumi WebGL output with index.html at ZIP root."""
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parents[1]
build = root / "Builds" / "WebGL"
destination = root / "Builds" / "Sumi-itch.zip"
if not (build / "index.html").is_file():
    raise SystemExit("No WebGL build index.html found")
with ZipFile(destination, "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
    for source in sorted(build.rglob("*")):
        if source.is_file():
            archive.write(source, source.relative_to(build).as_posix())
print(destination)
