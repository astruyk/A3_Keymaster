"""
	Gathers a set of keys together and uploads them to an FTP server based on a JSON config file.
	
	See https://github.com/astruyk/A3_Keymaster/wiki for details.
"""

import sys;
import os;
import urllib.request;
import json;
import re;
import io;
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
mods = [];
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
			if (modNameMatch and not (modNameMatch.group(1).lower() in mods)):
				mods.append(modNameMatch.group(1).lower());
if 'manualMods' in configJson:
	for modName in map(str.lower, configJson['manualMods']):
		if not modName in mods:
			mods.append(modName);

# Check to see if there is a .PAR file specified, and download it
parFileContents = '';
if ('parFileSource' in configJson) and ('parFileFtpPath' in configJson):
	print ("Grabbing PAR file...", end="");
	request = urllib.request.urlopen(configJson['parFileSource']);
	try:
		data = request.read();
		request.close();
		parFileContents = data.decode('utf-8');
	except Exception:
		print (traceback.format_exc());
		sys.exit(1);
	print ("Done.");
else:
	print ("No PAR file specified... Skipping.");

# Check to see if we need to add the -mod line in the PAR file
if (parFileContents != '') and ('parFileGenerateModParameter' in configJson) and (configJson['parFileGenerateModParameter'].lower() == 'true'):
	clientOnlyMods = [];
	serverSpecificMods = [];
	serverStartupMods = [];
	
	# Pull down the file that contains the list of client-only mods (if it is specified)
	if ('parFileClientOnlyModList' in configJson):
		print ("Grabbing list of client-only mods...", end="");
		request = urllib.request.urlopen(configJson['parFileClientOnlyModList']);
		try:
			data = request.read();
			request.close();
			clientOnlyModFileContents = json.loads(data.decode('utf-8'));
			for modName in clientOnlyModFileContents:
				clientOnlyMods.append(modName.lower());
		except Exception:
			print (traceback.format_exc());
			sys.exit(1);
		print ("Done.");
		
		print ("Retrieved " + str(len(clientOnlyMods)) + " client only mods:");
		for modName in clientOnlyMods:
			print ("\t" + modName);
	
	# Go through the list of mods in the server config file and add them to the list of mods for the server
	for modName in mods:
		if (not modName in clientOnlyMods):
			serverStartupMods.append(modName);
	
	# Go through the list of server specific mods (might not be in mod list) and add them to the list
	# of mods for the server to start with.
	for modName in serverSpecificMods:
		if not (modName in clientOnlyMods):
			clientOnlyMods.append(modName);
	print ("Found " + str(len(serverStartupMods)) + " mods that need to be in server startup command:");
	for modName in serverStartupMods:
		print ("\t" + modName);

	# Generate a new command line to specify the mods
	modCommandLine = 'mod="-mod=' + ";".join(serverStartupMods) + '";';
	
	# Modify the existing PAR file with the new mod line
	newParFileContents = '';
	for line in parFileContents.split('\r\n'):
		if '-mod=' in line:
			line = "// Disabled by automated script. Using generated value.\n//" + line;
		if '};' in line:
			line = "// Generated command line:\n\t" + modCommandLine + "\n" + line;
		newParFileContents += line + "\n";
	parFileContents = newParFileContents;

# Generate the set of keys that are required
print ("Looking up keys for " + str(len(mods)) + " mods.");
requiredKeys = {}; #mapping of key name to array of mods that require it (for reporting)

# Add keys that were associated with mods.
missingMappingEntry = set();
for modName in mods:
	if modName in mappingJson:
		for key in mappingJson[modName]:
			if (key in requiredKeys):
				requiredKeys[key].append(modName);
			else:
				requiredKeys[key] = [modName];
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
	for key in configJson['manualKeys']:
		if (key in requiredKeys):
			requiredKeys[key].append('Manual');
		else:
			requiredKeys[key] = ['Manual'];

# Download the keys from the keystore
print ("Grabbing " + str(len(requiredKeys.keys())) + " key(s) needed for update:");
if not os.path.exists('tmp'):
	os.makedirs('tmp');
for keyName in sorted(requiredKeys.keys()):
	keyUrl = configJson['keyLocation'] + "/" + urllib.parse.quote(keyName);
	print ("\t" + keyName + " (" + ",".join(requiredKeys[keyName]) + ")");
	request = urllib.request.urlretrieve(keyUrl, 'tmp/' + keyName);
print ("Done.");

# Connect to the server and update the keys as necessary
print ('Connecting to FTP server...', end="");
with FTP(configJson['ftpAddress']) as ftp:
	ftp.login(configJson['ftpUser'], configJson['ftpPassword']);
	ftp.cwd(configJson['ftpPath']);
	print ("Connected.");
	
	# Update the keys on the server...
	print ('Checking keys on server:');
	filesOnServerAtStart = ftp.nlst();
	for existingFile in filesOnServerAtStart:
		print ("\tRemoving existing key: " + existingFile + " ...", end="");
		ftp.delete(existingFile);
		print ("Done.")
	for requiredKey in requiredKeys.keys():
		print ("\tUploading key: " + requiredKey + " ...", end="");
		with open("tmp/" + requiredKey, "rb") as file:
			ftp.storbinary('STOR ' + requiredKey, file);
		print ("Done.");
	
	# Update the PAR file if we have contents for that
	if (parFileContents != ''):
		print ("Updating PAR file ...");
		ftp.cwd(os.path.dirname(configJson['parFileFtpPath']));
		filesInParDir = ftp.nlst();
		parFileName = os.path.basename(configJson['parFileFtpPath']);
		if (parFileName in filesInParDir):
			print ("\tRemoving old PAR file ...", end="");
			ftp.delete(parFileName);
			print ("Done.");
		print ("\tUploading new PAR file ...", end="");
		file = io.BytesIO(parFileContents.encode("utf-8"))
		ftp.storbinary('STOR ' + parFileName, file);
		print ("Done.");
		print ("Done.");
print ("");
print ("Operation was a success!! Congratulations.");