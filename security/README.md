Security
========


Verifying SyncTrayzor Releases
------------------------------

How do you know that the SyncTrayzor release you're downloading is genuine?

Every release is accompanied by a `sha512sum.txt.asc` file.
This contains the SHA-512 digest of all released files, and a PGP signature.
That signature was created by SyncTrayzor's release key, available [here](synctrayzor_releases_cert.asc).

This means that you can verify a release file by performing the following steps:

#### Once-off:

- Download the [SyncTrayzor release key](synctrayzor_releases_cert.asc) into your keychain.

#### For every release:

1. Download the release file you're interested in, and the `sha512sum.txt.asc` file.
2. Verify that the `sha512sum.txt.asc` file was signed by the SyncTrayzor release key.
3. Verify that the sha1 hash of the release file you downloaded matches the value in `sha512sum.txt.asc`.

For example:

``` sh
# Download the Syncthing release key
wget https://raw.githubusercontent.com/GermanCoding/SyncTrayzor/refs/heads/main/security/synctrayzor_releases_cert.asc

# Import the key
gpg --import synctrayzor_releases_cert.asc

# <Download the release files and the sha512sum.txt.asc file>

# Check the signature on sha512sum.txt.asc
gpg --verify sha512sum.txt.asc # Should output some message saying "Good signature"

# Validate the checksums of the (correctly signed) file
sha512sum -c sha512sum.txt.asc
# Should indicate "OK" for all files that you downloaded
```


Automatic Update Security
-------------------------

Every automatically downloaded update is verified in a similar way to the procedure outlined above.

SyncTrayzor contains the certificate of the SyncTrayzor Release Key.
When it downloads an update, it will also download the `sha512sum.txt.asc` file for that release.
It will then verify signature on the `sha512sum.txt.asc` file using the certificate it has, before checking that the sha512sum of the downloaded update matches that in the `sha512sum.txt.asc` file.

If either of these checks fails, then both files are deleted.

This means that only updates which are 1) not corrupt, and 2) were signed by the SyncTrayzor release private key are installed.

As part of the build process, Syncthing binaries are downloaded and are bundled with the SyncTrayzor installer.
A similar check is carried out here: SyncTrayzor contains Syncthing's release key, and verifies that the Syncthing binaries were released by the owner of that key.