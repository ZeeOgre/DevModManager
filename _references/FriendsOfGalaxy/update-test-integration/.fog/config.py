# -------- consts ---------

# url of original repository from github
UPSTREAM = 'https://github.com/FriendsOfGalaxyTester/test-integration'
# branch to be checked for new updates
RELEASE_BRANCH = 'master'
# integration source directory, where the manifest.json is placed; relative to root repo dir
SRC = '.'

# --------- jobs -----------

# if pack job is not defined, simple zip files will be produced from SRC
# def pack(output):
#   pass
