import os
import json
from invoke import task
from plugin import __version__
from github import Github


@task
def release(c):
    token = os.environ['GITHUB_TOKEN']
    g = Github(token)
    repo = g.get_user().get_repo('test-integration')
    branch = repo.default_branch
    release = repo.create_git_release(
        __version__, f"Release v{__version__}", "Autorelease",
        prerelease=False, target_commitish=branch
    )
    print('Release created: ', release)


@task
def autoincrement(c):
    with open('manifest.json', 'r') as f:
        manifest = json.load(f)

    currver = manifest['version']
    major, minor = currver.split('.')

    newver = '.'.join([major, str(int(minor) + 1)])
    manifest['version'] = newver

    with open('manifest.json', 'w') as f:
        json.dump(manifest, f, indent=4)
