using System;
using System.IO;
using System.Text.RegularExpressions;

var file = @"C:\dev\ScreenTranslator\App.xaml.cs";
var content = File.ReadAllText(file);
var newContent = Regex.Replace(content, @"File\.AppendAllText\(@""C:\\dev\\ScreenTranslator\\debug_startup\.log"", (.*?)\);", "ScreenTranslator.Services.SafeLogger.Log($1);");
File.WriteAllText(file, newContent);
