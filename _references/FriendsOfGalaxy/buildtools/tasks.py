import os
import shutil
from invoke import task


@task
def release(c):
    if os.path.exists('dist'):
        print('removing dist/')
        shutil.rmtree('dist')
    c.run('python setup.py sdist bdist_wheel')
    c.run('twine upload dist/*')

@task
def test(c):
    c.run('python -m pytest tests/ -v')
