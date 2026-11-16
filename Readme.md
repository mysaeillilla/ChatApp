To build a **Chat Application using Flutter**, I recommend following a modular architecture where the Flutter application handles the UI, a backend API manages business logic, and a real-time communication service handles instant messaging.

Since you're familiar with Flutter and .NET 8 Web API, I'll structure this using:

* **Frontend:** Flutter
* **Backend:** ASP.NET Core Web API (.NET 8)
* **Real-time communication:** SignalR (WebSockets)
* **Database:** PostgreSQL or MySQL
* **Authentication:** JWT
* **File storage:** Local storage initially, expandable to cloud storage.

# **1. Overall architecture**

[Guide to the Chat Architecture. Based on Examples of Popular Services | Yellow](https://images.openai.com/static-rsc-4/FatnQK4Yyv5cptMrAK3gTY76oPxToTpeaNyRc8S5CU6-N7S0ja-0MVJhLJREIfLiEiRdNyE6CIMWtkfEfU8rzHXoZoZWuVOiI8oDiAwjc7z2eDBu-LceQqf2cdk8XOpuVjooYoEAHRF-sDGIGtAe08qYuAFV6onzogYAfhrNp_I?purpose=inline)

[yellow.systems](https://yellow.systems/blog/guide-to-the-chat-architecture?utm_source=chatgpt.com)

```
              FLUTTER MOBILE / DESKTOP
                       |
          +------------+------------+
          |                         |
       REST API                 SignalR
          |                    WebSocket
          |                         |
          +------------+------------+
                       |
               ASP.NET CORE API
                       |
       +---------------+----------------+
       |               |                |
   Auth Service   Chat Service     File Service
       |               |                |
       +---------------+----------------+
                       |
                   DATABASE
                   PostgreSQL
```

# **2. Features to implement**

I recommend developing the application in phases rather than implementing everything at once.

**Phase 1 – Basic chat application**

Start here

[FlyChat Chatting App UI Design📱. :: Behance](https://images.openai.com/static-rsc-4/IxFnz8cdnJTCYG2IHpdi4iDq-0GZegePHeMKAenD6xvBwBGwESm-UrWjaXJR4_NRqLRuEqpP6t2D1HdjtvVpcsIHadXQrR9PzCsuLE_b_Ag0VrOvcBoFvLDQAlrTwHAoa-6wM6cf7fQgK8VAg_hztQ41524F2oWD4gk_cbCZins?purpose=inline)

**Authentication**

Login, registration, JWT authentication

[Nina Skrbic | Dribbble](https://images.openai.com/static-rsc-4/h-RWZXirH4e3omUYYWn6bSRRO_tTbNeBjfWvSeWVNPLkgLC3PdWYlevldHI0T7YKp7xxzaQI9Or0-xrD98eiJDazCdmO_LW6nYD7lLNFRdwrme-tPEi2zCDfRjx43W7oygo4SyMZrucEqo_rgCtPg6xJnh8rCNXrO1OGymORf6M?purpose=inline)

**Chat list**

Recent conversations, last message, timestamps

[Display Message App at Odessa Anderson blog](https://images.openai.com/static-rsc-4/-IajU92BI1EE--obw10T6iB1Du39VQF6xtc4_FBKVaPsyxgvcou17i_mQl5DkEzXDZfvy3JKxm5WFj8qtAQC09kQl7ea-Tpnh8JEcRrsIw6GSribtgyvA2AG1_jIAsYGXGpxZt58K5lbSVebkCHwCI3tDU5v-XPjsmOZ-HZmO0g?purpose=inline)

**One-to-one chat**

Send and receive real-time messages

[Profile and Chat box Screens Design by Sonum Khalil on Dribbble](https://images.openai.com/static-rsc-4/PaEZ5DQYssdnEdmIQrEraiChMyIbCVjEbPDcvac4vSIebW57MUKnfJKqGnQZH717eOTbicWxzVxAXmD1vfUy_Coqbogjsw0cMv4srUqBmtzaRVLN0TAMZjs2jJbJJBkJuBzkUdQzPnLnBGiYyBF4zoa9zr8pDi3nM2_iD2jYRtY?purpose=inline)

**User profile**

Username, profile picture, status

**Phase 2 – Advanced messaging**

* Group conversations
* Image and document sharing
* Message reply and forwarding
* Message editing and deletion
* Message delivery status
* Read receipts
* Typing indicators
* Online/offline status

  **Phase 3 – Additional features**
* Audio and video calls (WebRTC)
* Push notifications (Firebase Cloud Messaging)
* Message search
* Message reactions
* Voice messages
* End-to-end encryption

# **3. Flutter project folder structure**

Use a feature-based architecture instead of putting everything inside a single `<span>screens</span>` folder.

```
chat_app/
│
├── lib/
│   │
│   ├── main.dart
│   │
│   ├── app/
│   │   ├── app.dart
│   │   ├── routes.dart
│   │   └── theme.dart
│   │
│   ├── core/
│   │   ├── constants/
│   │   ├── exceptions/
│   │   ├── network/
│   │   │   ├── api_client.dart
│   │   │   └── signalr_client.dart
│   │   ├── storage/
│   │   │   └── secure_storage.dart
│   │   └── utils/
│   │
│   ├── models/
│   │   ├── user_model.dart
│   │   ├── message_model.dart
│   │   └── conversation_model.dart
│   │
│   ├── features/
│   │
│   │   ├── auth/
│   │   │   ├── screens/
│   │   │   ├── widgets/
│   │   │   ├── services/
│   │   │   └── providers/
│   │   │
│   │   ├── chat/
│   │   │   ├── screens/
│   │   │   │   ├── chat_list_screen.dart
│   │   │   │   └── chat_screen.dart
│   │   │   ├── widgets/
│   │   │   │   ├── message_bubble.dart
│   │   │   │   ├── message_input.dart
│   │   │   │   └── chat_app_bar.dart
│   │   │   ├── services/
│   │   │   ├── providers/
│   │   │   └── repositories/
│   │   │
│   │   ├── contacts/
│   │   ├── groups/
│   │   ├── profile/
│   │   └── settings/
│   │
│   └── shared/
│       ├── widgets/
│       └── extensions/
│
└── pubspec.yaml
```

### **Recommended Flutter packages**


| Package                                                  | Purpose               |
| -------------------------------------------------------- | --------------------- |
| `<span>http</span>`or`<span>dio</span>`                  | REST API requests     |
| `<span>signalr_netcore</span>`                           | Real-time messaging   |
| `<span>provider</span>`or`<span>flutter_riverpod</span>` | State management      |
| `<span>go_router</span>`                                 | Navigation            |
| `<span>flutter_secure_storage</span>`                    | Store JWT securely    |
| `<span>image_picker</span>`                              | Select images         |
| `<span>file_picker</span>`                               | Select documents      |
| `<span>cached_network_image</span>`                      | Display cached images |
| `<span>firebase_messaging</span>`                        | Push notifications    |

I recommend Riverpod for state management.

# **4. Backend (.NET 8) folder structure**

Keep the backend independent from the Flutter frontend.

```
ChatServer/
│
├── ChatServer.API/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── UsersController.cs
│   │   ├── ConversationsController.cs
│   │   ├── MessagesController.cs
│   │   └── FilesController.cs
│   │
│   ├── Hubs/
│   │   └── ChatHub.cs
│   │
│   ├── Middleware/
│   ├── Program.cs
│   └── appsettings.json
│
├── ChatServer.Core/
│   ├── Interfaces/
│   ├── DTOs/
│   ├── Services/
│   └── Validators/
│
├── ChatServer.Domain/
│   ├── Entities/
│   └── Enums/
│
├── ChatServer.Infrastructure/
│   ├── Data/
│   ├── Repositories/
│   ├── Migrations/
│   └── Services/
│
└── ChatServer.sln
```

# **5. Database design**

For the initial version, I recommend PostgreSQL.

These are the main tables you will need.

**Users**


| Column       | Type      |
| ------------ | --------- |
| Id           | UUID      |
| Username     | VARCHAR   |
| Email        | VARCHAR   |
| PasswordHash | VARCHAR   |
| ProfileImage | VARCHAR   |
| IsOnline     | BOOLEAN   |
| LastSeen     | TIMESTAMP |

**Conversations**


| Column    | Type      |
| --------- | --------- |
| Id        | UUID      |
| Name      | VARCHAR   |
| IsGroup   | BOOLEAN   |
| CreatedAt | TIMESTAMP |

**ConversationMembers**


| Column         | Type      |
| -------------- | --------- |
| Id             | UUID      |
| ConversationId | UUID      |
| UserId         | UUID      |
| JoinedAt       | TIMESTAMP |

**Messages**


| Column           | Type      |
| ---------------- | --------- |
| Id               | UUID      |
| ConversationId   | UUID      |
| SenderId         | UUID      |
| Content          | TEXT      |
| MessageType      | VARCHAR   |
| SentAt           | TIMESTAMP |
| EditedAt         | TIMESTAMP |
| ReplyToMessageId | UUID      |

**MessageReceipts**


| Column      | Type      |
| ----------- | --------- |
| Id          | UUID      |
| MessageId   | UUID      |
| UserId      | UUID      |
| DeliveredAt | TIMESTAMP |
| ReadAt      | TIMESTAMP |

**Attachments**


| Column    | Type    |
| --------- | ------- |
| Id        | UUID    |
| MessageId | UUID    |
| FileName  | VARCHAR |
| FilePath  | VARCHAR |
| FileType  | VARCHAR |
| FileSize  | BIGINT  |

### **Database relationships**

#chatgpt-mermaid-\_r\_nl\_{font-family:-apple-system-body,ui-sans-serif,-apple-system,system-ui,"Segoe UI",Helvetica,"Apple Color Emoji",Arial,sans-serif,"Segoe UI Emoji","Segoe UI Symbol";font-size:16px;fill:rgb(13, 13, 13);}@keyframes edge-animation-frame{from{stroke-dashoffset:0;}}@keyframes dash{to{stroke-dashoffset:0;}}#chatgpt-mermaid-\_r\_nl\_ .edge-animation-slow{stroke-dasharray:9,5!important;stroke-dashoffset:900;animation:dash 50s linear infinite;stroke-linecap:round;}#chatgpt-mermaid-\_r\_nl\_ .edge-animation-fast{stroke-dasharray:9,5!important;stroke-dashoffset:900;animation:dash 20s linear infinite;stroke-linecap:round;}#chatgpt-mermaid-\_r\_nl\_ .error-icon{fill:rgb(243, 243, 243);}#chatgpt-mermaid-\_r\_nl\_ .error-text{fill:rgb(13, 13, 13);stroke:rgb(13, 13, 13);}#chatgpt-mermaid-\_r\_nl\_ .edge-thickness-normal{stroke-width:1px;}#chatgpt-mermaid-\_r\_nl\_ .edge-thickness-thick{stroke-width:3.5px;}#chatgpt-mermaid-\_r\_nl\_ .edge-pattern-solid{stroke-dasharray:0;}#chatgpt-mermaid-\_r\_nl\_ .edge-thickness-invisible{stroke-width:0;fill:none;}#chatgpt-mermaid-\_r\_nl\_ .edge-pattern-dashed{stroke-dasharray:3;}#chatgpt-mermaid-\_r\_nl\_ .edge-pattern-dotted{stroke-dasharray:2;}#chatgpt-mermaid-\_r\_nl\_ .marker{fill:rgb(143, 143, 143);stroke:rgb(143, 143, 143);}#chatgpt-mermaid-\_r\_nl\_ .marker.cross{stroke:rgb(143, 143, 143);}#chatgpt-mermaid-\_r\_nl\_ svg{font-family:-apple-system-body,ui-sans-serif,-apple-system,system-ui,"Segoe UI",Helvetica,"Apple Color Emoji",Arial,sans-serif,"Segoe UI Emoji","Segoe UI Symbol";font-size:16px;}#chatgpt-mermaid-\_r\_nl\_ p{margin:0;}#chatgpt-mermaid-\_r\_nl\_ .entityBox{fill:rgb(222, 234, 251);stroke:rgb(83, 154, 248);}#chatgpt-mermaid-\_r\_nl\_ .relationshipLabelBox{fill:rgb(243, 243, 243);opacity:0.7;background-color:rgb(243, 243, 243);}#chatgpt-mermaid-\_r\_nl\_ .relationshipLabelBox rect{opacity:0.5;}#chatgpt-mermaid-\_r\_nl\_ .labelBkg{background-color:rgba(243, 243, 243, 0.5);}#chatgpt-mermaid-\_r\_nl\_ .edgeLabel{background-color:rgb(252, 252, 252);}#chatgpt-mermaid-\_r\_nl\_ .edgeLabel .label rect{fill:rgb(252, 252, 252);}#chatgpt-mermaid-\_r\_nl\_ .edgeLabel .label text{fill:rgb(13, 13, 13);}#chatgpt-mermaid-\_r\_nl\_ .edgeLabel .label{fill:rgb(83, 154, 248);font-size:14px;}#chatgpt-mermaid-\_r\_nl\_ .label{font-family:-apple-system-body,ui-sans-serif,-apple-system,system-ui,"Segoe UI",Helvetica,"Apple Color Emoji",Arial,sans-serif,"Segoe UI Emoji","Segoe UI Symbol";color:rgb(13, 13, 13);}#chatgpt-mermaid-\_r\_nl\_ .edge-pattern-dashed{stroke-dasharray:8,8;}#chatgpt-mermaid-\_r\_nl\_ .node rect,#chatgpt-mermaid-\_r\_nl\_ .node circle,#chatgpt-mermaid-\_r\_nl\_ .node ellipse,#chatgpt-mermaid-\_r\_nl\_ .node polygon{fill:rgb(222, 234, 251);stroke:rgb(83, 154, 248);stroke-width:1px;}#chatgpt-mermaid-\_r\_nl\_ .relationshipLine{stroke:rgb(143, 143, 143);stroke-width:1px;fill:none;}#chatgpt-mermaid-\_r\_nl\_ .marker{fill:none!important;stroke:rgb(143, 143, 143)!important;stroke-width:1;}#chatgpt-mermaid-\_r\_nl\_ [data-look=neo].labelBkg{background-color:rgba(243, 243, 243, 0.5);}#chatgpt-mermaid-\_r\_nl\_ .node .neo-node{stroke:rgb(83, 154, 248);}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node rect,#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].cluster rect,#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node polygon{stroke:url(#chatgpt-mermaid-\_r\_nl\_-gradient);filter:drop-shadow( 1px 2px 2px rgba(185,185,185,1));}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].swimlane.cluster rect{filter:none;}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node path{stroke:url(#chatgpt-mermaid-\_r\_nl\_-gradient);stroke-width:1px;}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node .outer-path{filter:drop-shadow( 1px 2px 2px rgba(185,185,185,1));}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node .neo-line path{stroke:rgb(83, 154, 248);filter:none;}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node circle{stroke:url(#chatgpt-mermaid-\_r\_nl\_-gradient);filter:drop-shadow( 1px 2px 2px rgba(185,185,185,1));}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].node circle .state-start{fill:#000000;}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].icon-shape .icon{fill:url(#chatgpt-mermaid-\_r\_nl\_-gradient);filter:drop-shadow( 1px 2px 2px rgba(185,185,185,1));}#chatgpt-mermaid-\_r\_nl\_ [data-look="neo"].icon-shape .icon-neo path{stroke:url(#chatgpt-mermaid-\_r\_nl\_-gradient);filter:drop-shadow( 1px 2px 2px rgba(185,185,185,1));}#chatgpt-mermaid-\_r\_nl\_ :root{--mermaid-font-family:-apple-system-body,ui-sans-serif,-apple-system,system-ui,"Segoe UI",Helvetica,"Apple Color Emoji",Arial,sans-serif,"Segoe UI Emoji","Segoe UI Symbol";}UsersConversationMembersConversationsMessagesMessageReceiptsAttachmentsjoinscontainscontainssendstrackshasreplies\_to

# **6. Real-time messaging using SignalR**

This is the most important part of the application.

REST APIs are useful for retrieving chat history, users and conversations. SignalR handles real-time message delivery.

### **Message flow**

[SignalR in .NET Core: Step‑by‑Step Guide to Real‑Time Communication | by Hafiz Muhammad Asad | Medium](https://images.openai.com/static-rsc-4/RECA2bVqTVlYYMByvnZuMUzv7t7QyUQIL9zJ4r_Eg4-jn7_t_b_0qsYYr1e2bWdTnehTJQZMXVFo-MK4_erJB-uAhxQsV-e95Pr3Pkv4RiC9tpFQbH8kOmP5nTZO1iR5WqUk--LR4hSb3UbrjBTCDF9xsy3fnepiNQ74D37JWx0?purpose=inline)

[medium.com](https://medium.com/%40asad072/signalr-in-net-core-step-by-step-guide-to-real-time-communication-460389115a7a?utm_source=chatgpt.com)

Example:

1. User A opens a conversation.
2. Flutter connects to the SignalR Hub.
3. User A sends a message.
4. SignalR sends the message to the backend.
5. Backend validates and saves it in PostgreSQL.
6. Backend sends the message to other conversation members.
7. User B receives the message immediately.

### **ChatHub.cs**

```
using Microsoft.AspNetCore.SignalR;

public class ChatHub : Hub
{
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            conversationId
        );
    }

    public async Task SendMessage(
        string conversationId,
        string message)
    {
        // Validate membership
        // Save message to database
        // Get authenticated sender ID

        await Clients.Group(conversationId)
            .SendAsync("ReceiveMessage", new
            {
                ConversationId = conversationId,
                Message = message
            });
    }
}
```

This is a simplified example. In production, derive the sender identity from the authenticated JWT, validate conversation membership, and persist the message before broadcasting.

### **Flutter SignalR connection**

```
final connection = HubConnectionBuilder()
    .withUrl(
      'https://localhost:5001/hubs/chat',
      options: HttpConnectionOptions(
        accessTokenFactory: () async => jwtToken,
      ),
    )
    .build();

await connection.start();

connection.on('ReceiveMessage', (arguments) {
  print('New message: $arguments');
});

await connection.invoke(
  'JoinConversation',
  args: ['conversation-id'],
);
```

# **7. API endpoint design**

Your REST API can follow this structure:


| Method | Endpoint                                      | Purpose             |
| ------ | --------------------------------------------- | ------------------- |
| POST   | `<span>/api/auth/register</span>`             | Register            |
| POST   | `<span>/api/auth/login</span>`                | Login               |
| GET    | `<span>/api/users</span>`                     | Get users           |
| GET    | `<span>/api/users/{id}</span>`                | Get user details    |
| POST   | `<span>/api/conversations</span>`             | Create conversation |
| GET    | `<span>/api/conversations</span>`             | Get chat list       |
| GET    | `<span>/api/messages/{conversationId}</span>` | Get message history |
| POST   | `<span>/api/messages</span>`                  | Send message        |
| PUT    | `<span>/api/messages/{id}</span>`             | Edit message        |
| DELETE | `<span>/api/messages/{id}</span>`             | Delete message      |
| POST   | `<span>/api/files/upload</span>`              | Upload attachment   |

SignalR Hub:

```
/hubs/chat
```

SignalR events:


| Event            | Description         |
| ---------------- | ------------------- |
| JoinConversation | Join a chat         |
| ReceiveMessage   | Receive new message |
| MessageDelivered | Message delivered   |
| MessageRead      | Message read        |
| UserTyping       | Typing indicator    |
| UserOnline       | User comes online   |
| UserOffline      | User goes offline   |

# **8. Flutter UI screens**

I would start with these five screens.

[Chat Room App UI by Ajendra Sutariya ✈ on Dribbble](https://images.openai.com/static-rsc-4/k8YUvmzSgjDP-RiknDmKzmmlTxojpxxVUI8k7yygBSjTE-j0lFABsaoJDGg1olaN7WqGXfWO2cp_ACYycP2y5m5jMqmBQNS79X1UdbgyKO2Pnlen_C2-RCBikZXHXRWZqLMReP_scdmqA8xM6MjhTGCsRARRlxvJjyfeYlR4PdA?purpose=inline)

**1. Login screen**

Email, password, login and registration.

[emenemtech](https://images.openai.com/static-rsc-4/fuCZNhAzIDy9hKkae-KH1G2c252C2qIlA2X_WsWAjcN_4qStR9xv5vsQntWW_jidLgBlmoyzFh6tYT77VhfPywO_hH4nR-MWnd46yj7R84it4FcZQ8Ju3KV2D4jZi4AsnhTEoRxNBRUgMDyPGUlBwldpqo5CtIY90OVknW-pCvk?purpose=inline)

**2. Chat list**

Recent conversations and unread counts.

[About | PenPalk](https://images.openai.com/static-rsc-4/BcnWhp-q7kB2SwNaQpQaAuIo7JQguzp2QaA6ueDrqnjyuXld5gDT6ThpbF2N8_sLz5WpD6YFCuHF3HV7krkONaFQCvUaHO6JTEv3o3dBk0mjX6SvRT9Bmz3x_TFGWT3-zjt5-i_WPBBhQCH5_sKpfDvjD9wTcwumkz2A-Tw_Cys?purpose=inline)

**3. Chat screen**

Message bubbles, attachments and input field.

[Day 11 of Daily UI: Messaging Interface for a Communication App by Sayali Kedare on Dribbble](https://images.openai.com/static-rsc-4/XBtvJUISaoZWY7FD5GbC1gRWBJ0TnYslepyaVSw302PVUocP-86_T9RPedVSKiQaq8ri2VxWhJDZQbL49EcBz1Yjd9UR8GDlAN43jZf7T_-ksi2W9QDa5MoEeAdTkPyOT79saNjm-FIRfE8xLiNwUdpBdBQFnPZh3AtGoXconvc?purpose=inline)

**4. Contacts**

Search and start new conversations.

[Dropset Login Flow Screenshots & UX Patterns | Uiland](https://images.openai.com/static-rsc-4/tn_dvHt10U1oTKy6sPyAdR8vqWjlqMfRqTXOTg-8lI9rXYw9B1dH8vKygZBDAbSzwQ2xlWSgGXvIcYy81Wwqm4TdAyk30adcXtTQoAvlJEc8Tlhbb5Woxfl9fJZ-N7qeJMTt26p4guLsgUOt27KW0oLg4A1w7b52fsuHnlye3o4?purpose=inline)

**5. Profile**

Profile picture, username and settings.

# **9. Development roadmap**

Here is the order I recommend following.


| Step | Development task                 | Priority |
| ---- | -------------------------------- | -------- |
| 1    | Create Flutter project           | High     |
| 2    | Create .NET Web API              | High     |
| 3    | Configure PostgreSQL and EF Core | High     |
| 4    | Implement JWT authentication     | High     |
| 5    | Build login and registration UI  | High     |
| 6    | Implement user listing           | High     |
| 7    | Create conversations and members | High     |
| 8    | Implement SignalR                | High     |
| 9    | Build real-time chat screen      | High     |
| 10   | Implement message history        | High     |
| 11   | Add delivery/read receipts       | Medium   |
| 12   | Add file sharing                 | Medium   |
| 13   | Add group chat                   | Medium   |
| 14   | Add push notifications           | Medium   |

# **10. My recommended technology stack**


| Layer             | Technology                 |
| ----------------- | -------------------------- |
| Mobile/Desktop UI | Flutter                    |
| State management  | Riverpod                   |
| Navigation        | GoRouter                   |
| Backend           | ASP.NET Core 8             |
| Real-time         | SignalR                    |
| Database          | PostgreSQL                 |
| ORM               | Entity Framework Core      |
| Authentication    | JWT                        |
| File storage      | Local filesystem initially |
| Notifications     | Firebase Cloud Messaging   |
| API testing       | Swagger / Postman          |
| Version control   | Git                        |

**One important architectural recommendation:** Don't use SignalR as your database or message storage. Save every message in PostgreSQL first, then broadcast it through SignalR. This ensures that users can retrieve messages even if they were offline when the message was sent.
