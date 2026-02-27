from setuptools import setup, find_packages

from src.fog.buildtools import __version__


with open("README.md", 'r') as f:
    long_description = f.read()


with open('requirements/app.txt') as f:
    dependencies = f.read().splitlines()


setup(
    name="fog.buildtools",
    version=__version__,
    license="MIT",
    description="Build tools for GOG Galaxy 2.0 plugins",
    long_description=long_description,
    long_description_content_type="text/markdown",
    author='FriendsOfGalaxy',
    author_email='FriendsOfGalaxy@gmail.com',
    packages=find_packages('src'),
    package_dir={'': 'src'},
    install_requires=dependencies,
    classifiers=[
        'Development Status :: 4 - Beta',
        'Intended Audience :: Developers',
        'Topic :: Software Development :: Build Tools',
        'License :: OSI Approved :: MIT License',
        'Programming Language :: Python :: 3',
    ],
    python_requires='>=3.6',
)
