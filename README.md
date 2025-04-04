Create a new project at https://console.cloud.google.com/projectcreate
Create a new service account: https://console.cloud.google.com/iam-admin/serviceaccounts
Create a new new key and download in JSON format.
Put this key to : LoxoneNet\service-account.json

Create a new project at https://console.home.google.com/projects/
Add a could-to-cloud integration
Put the address to the 
- Authorization URL: https://myhost.example.com:8082/auth
- Token Token URL: https://myhost.example.com:8082/token
- Cloud fulfillment URL: https://myhost.example.com:8082/webhook

In the Google Home app add a new device (Compatible with Google Home), select the service which was added in the prevous step. (It is marked as "[Test]")

You need a fix hostname (or any dyndns address) or fix ip for the Google callbacks.
You need a Let's encrypt certificate or forward the external TCP port 80 to port the service machine port 81. (The program gets a new certificate automatically from Let's Encrypt)

Configure:
- appsettings.json
- rooms.txt
- devices.txt

Contact me if you need any help.