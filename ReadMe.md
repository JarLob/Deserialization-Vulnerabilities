Binary formatter payload in the demo is based on the blog post and PoC of James Forshaw. The xml payload was generated with [ysoserial.net](https://github.com/pwntester/ysoserial.net) by Alvaro Muñoz.

1. Set the active project to "Serialize". Build and run. It will generate two files: payload.dat and payload2.dat in `c:\temp\` directory (adjust it for you specific setup).
2. Set the active project to "Deserialize". Run it. Change the payload file name or uncomment xml deserialization. Rebuild the "Deserialize" project. Run again.