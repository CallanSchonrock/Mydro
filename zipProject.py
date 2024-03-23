import os
import zipfile

def IsPathValid(path, ignoreDir, ignoreExt):
    splited = None
    if os.path.isfile(path):
        if ignoreExt:
            _, ext = os.path.splitext(path)
            if ext in ignoreExt:
                return False

        splited = os.path.dirname(path).split('\\/')
    else:
        if not ignoreDir:
            return True
        splited = path.split('\\/')

    if ignoreDir:
        for s in splited:
            if s in ignoreDir:  # You can also use set.intersection or [x for],
                return False

    return True


def zipDirHelper(path, rootDir, zf, ignoreDir=None, ignoreExt=None):
    # zf is zipfile handle
    if os.path.isfile(path):
        if IsPathValid(path, ignoreDir, ignoreExt):
            relative = os.path.relpath(path, rootDir)
            zf.write(path, relative)
        return

    ls = os.listdir(path)
    for subFileOrDir in ls:
        if not IsPathValid(subFileOrDir, ignoreDir, ignoreExt):
            continue

        joinedPath = os.path.join(path, subFileOrDir)
        zipDirHelper(joinedPath, rootDir, zf, ignoreDir, ignoreExt)


def ZipDir(path, zf, ignoreDir=None, ignoreExt=None, close=False):
    rootDir = path if os.path.isdir(path) else os.path.dirname(path)

    try:
        zipDirHelper(path, rootDir, zf, ignoreDir, ignoreExt)
    finally:
        if close:
            zf.close()

theZipFile = zipfile.ZipFile("QMydro.zip", 'w')
ZipDir("QMydro", theZipFile, ignoreDir=["delineateCatch"], ignoreExt=[".zip"], close=True)