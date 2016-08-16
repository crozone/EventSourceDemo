# EventSourceDemo
A demo event source server written as an ASP.NET Core MVC 6 application.

The server hosts an event source at the URL `/EventSource`, which pushes down a JSON object containing the current time as a Message, every second.

The Index for the site contains javascript which listens to this eventsource and appends each message as an element to a div tag.
