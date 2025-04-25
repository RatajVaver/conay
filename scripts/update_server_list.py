import os
import sys
import glob
import json
from operator import itemgetter

os.chdir(os.path.dirname(os.path.abspath(sys.argv[0])))

servers = []
ranks = { "halcyon": 5, "crossroads": 4, "chains": 3, "wod": 2, "tyranny": 1 }

for filename in glob.glob("../servers/*.json"):
    with open(filename, "r") as content:
        serverData = json.load(content)
        serverFile = os.path.basename(filename).replace(".json", "")
        servers.append({ "name": serverData['name'], "file": serverFile, "rank": ranks.get(serverFile, 0) })

servers = sorted(servers, key=itemgetter("rank"), reverse=True)
for x in servers:
    del x['rank']

with open("../servers.json", "w") as indexFile:
    indexFile.write( json.dumps(servers, indent=4, sort_keys=True) )