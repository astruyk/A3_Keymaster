import sys;
import urllib.request;
import json;
import re;
import traceback;
from optparse import OptionParser;
from ftplib import FTP;

parser = OptionParser();
parser.add_option("-c", "--config", dest = "config", help = "Location of the JSON config file used to determine the keys to updload.");

(options, args) = parser.parse_args();
if (options.config == None ):
	print ("\nMissing required option 'config'.\n");
	parser.print_help();
	sys.exit(2);

# Download the config file
print ("Downloading config file....", end="");
request = urllib.request.urlopen(options.config);
try:
	data = request.read();
	request.close();
except Exception:
	print (traceback.format_exc());
	sys.exit(1);
print ("Done.");

# Parse it into JSON 
configJson = json.loads(data.decode('utf-8'));
# print (json.dumps(configJson, sort_keys=True, indent=4, separators=(',', ': ')));

# Verify the required keys are there.
if not "keyLocation" in configJson:
	print ("Missing required entry in config file: keyLocation");
	sys.exit(1);
if not "keyMappingFile" in configJson:
	print ("Missing required entry in config file: keyMappingFile");
	sys.exit(1);
if not "ftpAddress" in configJson:
	print ("Missing required entry in config file: ftpAddress");
	sys.exit(1);
if not "ftpUser" in configJson:
	print ("Missing required entry in config file: ftpUser");
	sys.exit(1);
if not "ftpPassword" in configJson:
	print ("Missing required entry in config file: ftpPassword");
	sys.exit(1);
if not "ftpPath" in configJson:
	print ("Missing required entry in config file: ftpPath");
	sys.exit(1);

# Download the mapping file
print ("Downloading mapping file...", end="");
request = urllib.request.urlopen(configJson['keyMappingFile']);
try:
	data = request.read();
	request.close();
except Exception:
	print (traceback.format_exc());
	sys.exit(1);
print ("Done.");

# Parse mapping file into JSON
mappingJson = json.loads(data.decode('utf-8'));
# print (json.dumps(mappingJson, sort_keys=True, indent=4, separators=(',', ': ')));
mappingJson = dict((key.lower(), value) for key,value in mappingJson.items());

# Generate a set of mods that are required based on the PW6 config file and any additional user-specified values
mods = set();
if 'playWithSixServerConfigUrl' in configJson:
	print ("Downloading play with six configuration file...", end="");
	request = urllib.request.urlopen(configJson['playWithSixServerConfigUrl']);
	try:
		data = request.read();
		request.close();
	except Exception:
		print (traceback.format_exc());
		sys.exit(1);
	print ("Done.");
	
	modRegex = re.compile('\s*-\s*"(@\w+)"');
	isInModList = False;
	for line in data.decode('utf-8').split('\n'):
		if line.startswith(':name: '):
			serverName = line[len(':name: '):];
		if (isInModList and line.startswith(':')):
			isInModList = False;
		if ':required_mods:' in line:
			isInModList = True;
		if ':allowed_mods:' in line:
			isInModList = True;
		if isInModList:
			modNameMatch = modRegex.match(line);
			if (modNameMatch):
				mods.add(modNameMatch.group(1).lower());
if 'manualMods' in configJson:
	mods.update(map(str.lower, configJson['manualMods']));

# Generate the set of keys that are required
print ("Looking up keys for " + str(len(mods)) + " mods.");
keys = set();

# Add keys that were associated with mods.
missingMappingEntry = set();
for modName in mods:
	if modName in mappingJson:
		keys.update(mappingJson[modName]);
	else:
		missingMappingEntry.add(modName);
if len(missingMappingEntry) > 0:
	print ("Unable to find keys associated with the following mods in lookup table (is it up to date?):");
	for modName in missingMappingEntry:
		print ("\t" + modName);
	print ("ERROR - Aborted due to missing keys.");
	sys.exit(1);

# Add any manually specified keys from the JSON file.
if 'manualKeys' in configJson:
	keys.update(configJson['manualKeys']);

print ("Found " + str(len(keys)) + " key(s) to add.");


"""

# Parse the downloaded file data to extract the list of mods
print ("Downloaded successfully. Parsing...", end="");
isInModList = False;
modRegex = re.compile('\s*-\s*"(@\w+)"');
mods = [];
for line in data.decode('utf-8').split('\n'):
	if line.startswith(':name: '):
		serverName = line[len(':name: '):];
	if (isInModList and line.startswith(':')):
		isInModList = False;
	if ':required_mods:' in line:
		isInModList = True;
	if ':allowed_mods:' in line:
		isInModList = True;
	if isInModList:
		modNameMatch = modRegex.match(line);
		if (modNameMatch):
			print ('.', end="");
			mods.append(modNameMatch.group(1).lower());
print ("Done.");

# print ("Server '" + serverName + "' with " + str(len(mods)) + " mods.");

print ('Connecting to FTP server...');
keySources = [];
with FTP(options.address) as ftp:
	ftp.login(options.login, options.password);
	ftp.cwd(options.directory);
	fileInfo = ftp.nlst();
	[x.lower() for x in fileInfo];
	for modName in mods:
		if (modName in fileInfo):
			print ("FOUND mod - " + modName);
		else:
			print ("NOT FOUND mod - " + modName);
"""