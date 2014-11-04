import sys;
import urllib.request;
import re;
from optparse import OptionParser;

parser = OptionParser();
parser.add_option("-c", "--pw6ConfigUrl", dest = "pw6ConfigUrl", help = "URL pointing to the PW6 server config YML to read the list of mods from.");
parser.add_option("-f", "--ftpAddress", dest = "ftpAddress", help = "FTP Address of the server to modify.");
parser.add_option("-l", "--loginName", dest = "ftpLogin", help = "User name to use when logging into the FTP.");
parser.add_option("-p", "--password", dest = "ftpPassword", help = "Password to use when logging into the FTP.");
parser.add_option("-d", "--ftpDir", dest = "ftpDirectory", help = "FTP folder where the root of the server is located.");

(options, args) = parser.parse_args();
"""
if (	options.pw6ConfigUrl == None
		or options.ftpAddress == None
		or options.ftpLogin == None
		or options.ftpPassword == None
		or options.ftpDirector == None):
	print ("\nMissing required option.\n");
	parser.print_help();
	sys.exit(2);
"""
print ("Downloading PW6 repo info from \n\t" + options.pw6ConfigUrl);
request = urllib.request.urlopen(options.pw6ConfigUrl);
print ("...");
try:
	data = request.read();
	request.close();
except Exception:
	import traceback;
	print (traceback.format_exc());
	sys.exit(1);

print ("Downloaded successfully. Parsing...");

isInModList = False;
modRegex = re.compile('\s*-\s*"(@\w+)"');

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
			print (modNameMatch.group[1]);
			mods += modNameMatch.group[1];

print ("Done parsing...");
		
print ("Server Name:" + serverName);
print ("Found " + mods.size + " mods:");
for modName in mods:
	print ("\t" + modName);