import sys;
import os;
import json;
from optparse import OptionParser;

parser = OptionParser();
parser.add_option("-i", "--inputDir", dest = "inputDir", help = "The local mod folder to use to generate the mapping.");
parser.add_option("-o", "--outputFile", dest = "outputFile", help = "The location of the key file to generate.");

(options, args) = parser.parse_args();
if (options.inputDir == None ):
	print ("\nMissing required option 'inputDir'.\n");
	parser.print_help();
	sys.exit(2);
if (options.outputFile == None ):
	print ("\nMissing required option 'ouputFile'.\n");
	parser.print_help();
	sys.exit(2);

if not os.path.exists(options.inputDir):
	print ("Mod directory (" + options.inputDir + ") does not exist.");
	sys.exit(1);

if not os.path.isdir(options.inputDir):
	print ("Mod directory (" + options.inputDir + ") is not a directory.");
	sys.exit(1);

modMappings = {};
for pathName in os.listdir(options.inputDir):
	modDir = os.path.join(options.inputDir, pathName);
	if (pathName.startswith("@") and os.path.isdir(modDir)):
		print ("Found mod folder: " + pathName);
		keysDirs = ["keys", "key"]; # Thanks JSRS...
		keys = [];
		for keyDirName in keysDirs:
			keysDir = os.path.join(modDir, keyDirName);
			if (os.path.exists(keysDir) and os.path.isdir(keysDir)):
				print ("\t\tExamining keys folder: " + keyDirName);
				for keyFile in os.listdir(keysDir):
					if keyFile.endswith('.bikey'):
						print ("\t\t\tKey file found: " + keyFile);
						keys.append(keyFile);
				print ("\tExtracted " + str(len(keys)) + " key(s).");
		if len(keys) > 0:
			modMappings[pathName] = keys;

jsonOutput = json.dumps(modMappings, sort_keys=True, indent=4, separators=(',', ':'));
with open(options.outputFile, "w") as outputFile:
    outputFile.write(jsonOutput)