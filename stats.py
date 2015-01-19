#!/usr/bin/env python
 
import sys
 
if (len(sys.argv) < 2):
    print("Usage: " + sys.argv[0] + " github-user [github-project]")
    exit(1)
 
try:
  import requests
except ImportError:
    print("Error: requests is not installed")
    print("Installing Requests is simple with pip:\n  pip install requests")
    print("More info: http://docs.python-requests.org/en/latest/")
    exit(1)
import io
import json
 
 
def dict_to_object(d):
    if '__class__' in d:
        class_name = d.pop('__class__')
        module_name = d.pop('__module__')
        module = __import__(module_name)
        class_ = getattr(module, class_name)
        args = dict((key.encode('ascii'), value) for key, value in d.items())
        inst = class_(**args)
    else:
        inst = d
    return inst
 
 
def ensure_str(s):
    if isinstance(s, unicode):
        s = s.encode('utf-8')
    return s
 
full_names = []
 
if len(sys.argv) == 3:
    full_names.append(sys.argv[1] + "/" + sys.argv[2])
else:
    buf = io.StringIO()
    r = requests.get('https://api.github.com/users/' + sys.argv[1] + '/repos')
    myobj = r.json()
 
    for rep in myobj:
        full_names.insert(0, ensure_str(rep['full_name']))
 
 
for full_name in full_names:
    buf = io.StringIO()
    try:
        r = requests.get('https://api.github.com/repos/' + full_name + '/releases')
        myobj = r.json()
 
        total_downloads = 0
        for p in myobj[::-1]:
            if "assets" in p:
                for asset in p['assets']:
                    total_downloads += asset['download_count']
                    print("Tag: %s\nFile: %s\nCreated at: %s" %
                    	  (p['tag_name'], asset['name'], asset['created_at']))
                    print("Downloads: %d" % asset['download_count'])
                    print("")
            else:
                print("No data")
        print('Total downloads: %d' % total_downloads)
    except:
        pass
