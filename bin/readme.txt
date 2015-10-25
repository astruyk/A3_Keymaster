This program is a quick script intended to make changing the keys on the server for different modlists as hands-free as possible.

See https://github.com/astruyk/A3_ServerManager/wiki for usage instructions and details.

Version 2.3.0
* Use the keys stored on the server instead of requiring hosted key store.

Version 2.2.0
* Add support for uploading extra files.
* Fix a threading issue that caused rare crash
* Placeholder tab for Mission managment.
* Some UI tweaks

Version 2.1.0
* Add support for forcing updates via. config file.

Version 2.0.1
Keymaster Version 2.0.1 Released
* Fix crash when pressing 'Go' when there was a process already underway
* Add option to supress debug messages (default - debug messages off)
* Cleanup a bunch of the output to be more readable
* Add option to do a 'dry run' without actually updating the server (experimental, currently unstable)
* Add support for blacklisting particular keys (for mods that are signed multiple times with keys that are very general)
* Add support for specifying mods that should be run only on the server (to be added to generated command line)
* Renamed .exe from 'Keymaster' to 'Gatekeeper' to match old script names (sorry for confusion!)

Version 2.0.0
* Total re-write of Gatekeeper in C# with GUI

Version 1.2.0
* Support updating -mod command in the .par file dynamically based on the mods being run
* Support downloading a list of 'client-only' mods to prevent certain mods from running on the server
* Minor wording tweaks in output messages and order change for PAR file processing

Version 1.1.0
* Renamed project from 'A3_ServerUpdated' to 'A3_Keymaster' because ghostbusters is cool.
* Added option (--keyDir) to allow specifying a directory to place the gathered keys in when updating server
* Added support for updating a .par file on the server when running gatekeeper

Version 1.0.0
* Initial Release