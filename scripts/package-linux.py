import tarfile
import os


def fix_perms(info):
    if os.path.basename(info.name) == "Conay":
        info.mode = 0o755
    return info


with tarfile.open("conay-linux.tar.gz", "w:gz") as tar:
    tar.add("ConayLinux", arcname=".", filter=fix_perms)
