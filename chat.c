
#include <stdio.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <stdlib.h>
#include <errno.h>
#include <string.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <netinet/in.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/sendfile.h>
#include <pthread.h> //for threading , link with lpthread
 
 
#define MAXCLIENT 1000

char ipData[MAXCLIENT][INET6_ADDRSTRLEN];
int flag[MAXCLIENT];
char clntName[INET6_ADDRSTRLEN];
char portName[6]; 

int insertIP(char* ip){
    int i;
    for(i = 0; i < MAXCLIENT; i++)
        if(flag[i] == 0)
        {
            strcpy(ipData[i], ip);
            flag[i]==1;
            return i;
        }
    return -1;
}
int reset(){
    int i;
    for(i = 0; i < MAXCLIENT; i++)
        flag[i] = 0;
}
char* getIP(int i){
    return ipData[i];
}
void deleteIP(int i)
{
    flag[i] = 0;
}




void *connection_handler(void *);
 
int main(int argc , char *argv[])
{
    int socket_desc , new_socket , c , *new_sock;
    struct sockaddr_in server , client;
    char *message;
    int id;
     
    //Create socket
    socket_desc = socket(AF_INET , SOCK_STREAM , 0);
    if (socket_desc == -1)
    {
        printf("Could not create socket");
    }
     
    //Prepare the sockaddr_in structure
    server.sin_family = AF_INET;
    server.sin_addr.s_addr = INADDR_ANY;
    server.sin_port = htons( 8888 );
     
    //Bind
    if( bind(socket_desc,(struct sockaddr *)&server , sizeof(server)) < 0)
    {
        puts("bind failed");
        return 1;
    }
    puts("bind done");
     
    //Listen
    listen(socket_desc , 3);
     
    //Accept and incoming connection
    puts("Waiting for incoming connections...");
    c = sizeof(struct sockaddr_in);
    while( (new_socket = accept(socket_desc, (struct sockaddr *)&client, (socklen_t*)&c)) )
    {
        puts("Connection accepted");
        if(inet_ntop(AF_INET,&client.sin_addr.s_addr,clntName,sizeof(clntName))!=NULL){

          printf("Client = %s/%d\n",clntName,ntohs(client.sin_port));
        } else {
            printf("Unable to get address\n");
        }

        pthread_t sniffer_thread;
        new_sock = malloc(1);
        *new_sock = new_socket;
         
        if( pthread_create( &sniffer_thread , NULL ,  connection_handler , (void*) new_sock) < 0)
        {
            perror("could not create thread");
            return 1;
        }
         
        //Now join the thread , so that we dont terminate before the thread
        //pthread_join( sniffer_thread , NULL);
        puts("Handler assigned");
    }
     
    if (new_socket<0)
    {
        perror("accept failed");
        return 1;
    }
     
    return 0;
}
 
/*
 * This will handle connection for each client
 * */
 void recvFile(int socket)
 {
    char buffer[BUFSIZ];
    int file_size, remain_data, len, read_size, id;
    FILE *received_file;
    char fileName[255];
    char *message;
    //get file name
    read_size = recv(socket , buffer , BUFSIZ , 0);
    strncpy(fileName, buffer, read_size);
    puts(fileName);
    write(socket , "OK" , 2);

    /* Receiving file size */
    recv(socket, buffer, BUFSIZ, 0);
    file_size = atoi(buffer);
    printf("%d\n",file_size );
    write(socket, "OK", 2);
    //fprintf(stdout, "\nFile size : %d\n", file_size);

    received_file = fopen(fileName, "w");
    if (received_file == NULL)
    {
           
        exit(EXIT_FAILURE);
    }

    remain_data = file_size;

    while (remain_data > 0){
        if((len = recv(socket, buffer, BUFSIZ, 0)) > 0)
        {
            fwrite(buffer, sizeof(char), len, received_file);
            remain_data -= len;
            fprintf(stdout, "Receive %d bytes and we hope :- %d bytes\n", len, remain_data);
        }
    }
    printf("finish\n");
    fclose(received_file);

    //sendID back to user   
    sprintf(buffer, "%d", insertIP(fileName));
    write(socket , buffer , strlen(buffer));

 }

void sendFile(int socket)
{

    socklen_t       sock_len;
    ssize_t len;
    struct sockaddr_in      server_addr;
    struct sockaddr_in      peer_addr;
    int fd, id;
    int sent_bytes = 0;
    char file_size[256];
    struct stat file_stat;
    int offset;
    int remain_data;
    char buffer[BUFSIZ];
    //recv file id
    /* Receiving file size */
    recv(socket, buffer, BUFSIZ, 0);
    id = atoi(buffer);

    //open file
    fd = open(ipData[id], O_RDONLY);
    if (fd == -1)
    {
            fprintf(stderr, "Error opening file --> %s", strerror(errno));

            exit(EXIT_FAILURE);
    }

    /* Get file stats */
    if (fstat(fd, &file_stat) < 0)
    {
            fprintf(stderr, "Error fstat --> %s", strerror(errno));

            exit(EXIT_FAILURE);
    }

    fprintf(stdout, "File Size: \n%d bytes\n", file_stat.st_size);

    sock_len = sizeof(struct sockaddr_in);
    sprintf(file_size, "%d", file_stat.st_size);

    /* Sending file size */
    len = send(socket, file_size, sizeof(file_size), 0);
    if (len < 0)
    {
          fprintf(stderr, "Error on sending greetings --> %s", strerror(errno));

          exit(EXIT_FAILURE);
    }

    fprintf(stdout, "Server sent %d bytes for the size\n", len);

    offset = 0;
    remain_data = file_stat.st_size;
    /* Sending file data */
    while (((sent_bytes = sendfile(socket, fd, &offset, BUFSIZ)) > 0) && (remain_data > 0))
    {
            fprintf(stdout, "1. Server sent %d bytes from file's data, offset is now : %d and remaining data = %d\n", sent_bytes, offset, remain_data);
            remain_data -= sent_bytes;
            fprintf(stdout, "2. Server sent %d bytes from file's data, offset is now : %d and remaining data = %d\n", sent_bytes, offset, remain_data);
    }

    close(socket);
  
}

void *connection_handler(void *socket_desc)
{
    //Get the socket descriptor
    int sock = *(int*)socket_desc;
    int read_size;
    char *message , client_message[2000];
    char mess[50];
    int id;
    int task = 0;
    //read command
    read_size = recv(sock , client_message , 1 , 0);
    switch(client_message[0])
    {
        case 'S':
            task = 0;
            recvFile(sock);
        break;
        case 'R':
            task = 1;
            sendFile(sock);
        break;
        default: break;
    }
     
    if(read_size == 0)
    {
       
        puts("Client disconnected");
        fflush(stdout);
    }
    else if(read_size == -1)
    {
        perror("recv failed");
    }
         
    //Free the socket pointer
    free(socket_desc);
     
    return 0;

}