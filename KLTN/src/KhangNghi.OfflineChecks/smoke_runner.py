"""Run after building the solution. Only invokes --validate-config, never --run."""
from pathlib import Path
import json
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent
runner = root / 'KhangNghi.EtlRunner/bin/Debug/net9.0/KhangNghi.EtlRunner.dll'
assert runner.is_file(), 'Build the solution first.'
environment = os.environ.copy()
for name in ['KHANGNGHI_DWH_CONNECTION', 'KHANGNGHI_DTEXEC_PATH', 'KHANGNGHI_MASTER_PACKAGE',
             'KHANGNGHI_SSAS_SERVER', 'KHANGNGHI_SSAS_DATABASE']:
    environment.pop(name, None)

def check(name, arguments, code):
    result = subprocess.run(['dotnet', str(runner), *arguments], env=environment,
                            capture_output=True, text=True, timeout=15)
    assert result.returncode == code, f'{name}: exit {result.returncode}, expected {code}'
    print(f'PASS: {name} (exit {code})')

with tempfile.TemporaryDirectory(prefix='runner-smoke-', dir=root / 'KhangNghi.OfflineChecks') as temporary:
    workspace = Path(temporary).resolve()
    assert workspace.is_relative_to(root)
    fake_engine = workspace / 'not-executed.exe'
    fake_package = workspace / 'not-executed.dtsx'
    fake_engine.write_text('offline test: never execute', encoding='utf-8')
    fake_package.write_text('<test />', encoding='utf-8')
    config = workspace / 'runner.json'
    settings = {
        'ConnectionStrings': {'DwhConnection': 'Server=OFFLINE_INVALID_HOST;Database=OFFLINE_TEST;Integrated Security=True;Connect Timeout=1;'},
        'EtlSettings': {'DtExecPath': str(fake_engine), 'MasterPackagePath': str(fake_package),
                        'SsasServer': 'OFFLINE_INVALID_SSAS', 'SsasDatabase': 'OFFLINE_TEST'}
    }
    check('missing action does not start ETL', [], 2)
    check('unknown action does not start ETL', ['--unknown', '--config', str(config)], 2)
    check('missing configuration is rejected', ['--validate-config', '--config', str(config)], 1)
    config.write_text(json.dumps(settings), encoding='utf-8')
    check('valid local config does not launch fake engine or contact invalid servers',
          ['--validate-config', '--config', str(config)], 0)
    settings['ConnectionStrings']['DwhConnection'] = 'Server=OFFLINE_INVALID_HOST;Integrated Security=True;'
    config.write_text(json.dumps(settings), encoding='utf-8')
    check('database must be explicit', ['--validate-config', '--config', str(config)], 1)
    config.write_text('{invalid json', encoding='utf-8')
    check('invalid JSON is rejected', ['--validate-config', '--config', str(config)], 1)
print('Passed 6 runner smoke checks. No --run invocation was made.')
