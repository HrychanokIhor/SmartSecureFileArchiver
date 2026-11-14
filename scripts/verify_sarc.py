"""Run SARC v1 acceptance checks using the supplied team fixtures."""
import json
import random
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

cli = Path(sys.argv[1]).resolve()
team = Path(sys.argv[2]).resolve()
refs = team / "reference"
password = json.loads((team / "team_config.json").read_text())["пароль_сеансу"]


def run(*args, error=None):
    result = subprocess.run(["dotnet", str(cli), *map(str, args)], capture_output=True, text=True)
    if error:
        assert result.returncode != 0 and result.stderr.startswith(error + ":"), result.stderr
    else:
        assert result.returncode == 0, result.stderr
    return result


with tempfile.TemporaryDirectory() as root:
    root = Path(root)
    for manifest in sorted(refs.glob("ref_*.json")):
        expected = json.loads(manifest.read_text())["вміст"]
        volumes = sorted(refs.glob(manifest.stem + "*.arc"), reverse=True)
        output = root / manifest.stem
        run("unpack-set", output, password, *volumes)
        assert {p.name for p in output.iterdir()} == set(expected), manifest
        for name, meta in expected.items():
            data = (output / name).read_bytes()
            assert len(data) == meta["розмір"] and zlib.crc32(data) == int(meta["crc32"], 16), name

    failures = json.loads((refs / "expected_failures.json").read_text())
    for name, fixture in failures.items():
        run("unpack-set", root / "rejected", password, refs / name, error=fixture["очікуваний_код"])
        assert not (root / "rejected").exists()

    inputs = {"noise.bin": random.Random(42).randbytes(150000), "empty.dat": b"",
              "small.txt": b"hello world " * 40, "one.dat": b"x"}
    for name, data in inputs.items():
        (root / name).write_bytes(data)
    run("pack-set", root / "archive.arc", password, team / "team_config.json", "-",
        *(root / name for name in inputs))
    volumes = sorted(root.glob("archive.part*.arc"), reverse=True)
    assert len(volumes) > 1 and all(v.stat().st_size <= 65536 for v in volumes)
    run("unpack-set", root / "restored", password, *volumes)
    for name, data in inputs.items():
        assert (root / "restored" / name).read_bytes() == data
    for bad, code, invalid in [(volumes[:-1], "E_VOLUMES", password),
                                ([*volumes, volumes[0]], "E_VOLUMES", password),
                                (volumes, "E_HMAC", "incorrect-test-password")]:
        run("unpack-set", root / "rejected", invalid, *bad, error=code)
        assert not (root / "rejected").exists()

print("SARC acceptance: six reference sets, six failure codes, multi-volume round trip and rejection checks passed.")
