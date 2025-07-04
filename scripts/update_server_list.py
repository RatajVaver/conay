import os
import sys
import glob
import json
from operator import itemgetter

os.chdir(os.path.dirname(os.path.abspath(sys.argv[0])))

servers = []
ranks = { "halcyon": 3, "crossroads": 6, "chains": 5, "tyranny": 4, "wildheart": 7, "neoneden": 5 }

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