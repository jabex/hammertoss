# hammertoss
A proof of concept in C# from the FireEyes report

https://www2.fireeye.com/rs/848-DID-242/images/rpt-apt29-hammertoss.pdf

### Requirements:
  - microsoft visual studio 2012 or latest version
  - a local web server where you can save a web page that has within at least one jpeg image 


[Uploader] 
##### Step 1
Set the path of the image where you should inject the payload

```sh
string file_in = @"C:\Program Files\EasyPHP-DevServer-14.1VC11\data\localweb\projects\test\lena.jpg";
```

##### Step 2
Get returned hashtag and split it into #[offset][salt]

[tDiscoverer]

##### Step 3
Open tDiscoverer project as privileged user

##### Step 4
Set environment variables using hashtag and your web page URL paths:

```sh
private static string _malurl = "http://127.0.0.1/projects/test/test.html";
private static string _pageref = "http://127.0.0.1/projects/test/";
private static int _offset = 0;
private static string _salt = "";
```
