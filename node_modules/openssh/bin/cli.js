#!/usr/bin/env node

const c = require('ansi-colors')
const args = process.argv.slice(2)

if (args.length === 0) {
  console.log(c.red('❌ Invalid command!'))
  showHelp()
  process.exit(1)
}

if (args[0] === 'serve') {
  // openssh serve 90@mypassword formatını kontrol et
  if (args[1] && args[1].includes('@')) {
    const [port, password] = args[1].split('@')
    startServerDirect(port, password)
  } else {
    startServer()
  }
} else if (args[0].includes('@')) {
  startClient(args)
} else {
  console.log(c.red('❌ Invalid command!'))
  showHelp()
  process.exit(1)
}

function showHelp() {
  console.log(c.cyan(`
🔐 Openssh CLI

Usage:
  openssh serve                         # Start server (interactive)
  openssh serve <port>@<password>       # Start server (direct)
  openssh <url>@<password>             # Client connection (cd protected)
  openssh <url>@<password> -dusecd     # Client connection (cd unprotected)

Examples:
  openssh serve
  openssh serve 90@mypassword
  openssh localhost:42215@password123
  openssh localhost:42215@password123 -dusecd
`))
}

function startServerDirect(port, password) {
  console.log(c.yellow('🚀 Starting Openssh server...'))
  
  if (!password) {
    console.log(c.red('❌ Password cannot be empty!'))
    process.exit(1)
  }
  
  const serverPort = port || 42215
  
  const express = require('express')
  const app = express()
  const server = app.listen(serverPort)
  
  require('../index.js')(server, {
    password: password,
    console: false
  })
  
  console.log(c.green(`✅ Server running! Port: ${serverPort} 🔥`))
  console.log(c.cyan(`💡 Connect with: openssh localhost:${serverPort}@${password}`))
}

function startServer() {
  console.log(c.yellow('🚀 Starting Openssh server...'))
  
  const readline = require('readline')
  const rl = readline.createInterface({input: process.stdin, output: process.stdout})
  
  rl.question(c.blue('Port (default 42215): '), (port) => {
    rl.question(c.blue('Password: '), (password) => {
      if (!password) {
        console.log(c.red('❌ Password cannot be empty!'))
        process.exit(1)
      }
      
      const serverPort = port || 42215
      console.log('\n')
      rl.close()
      
      const express = require('express')
      const app = express()
      const server = app.listen(serverPort)
      
      require('../index.js')(server, {
        password: password,
        console: false
      })
      
      console.log(c.green(`✅ Server running! Port: ${serverPort} 🔥`))
      console.log(c.cyan(`💡 Connect with: openssh localhost:${serverPort}@${password}`))
    })
  })
}

function startClient(args) {
  const connectionString = args[0]
  const usecd = !args.includes('-dusecd')
  
  if (!connectionString.includes('@')) {
    console.log(c.red('❌ Format: url@password'))
    process.exit(1)
  }
  
  const [url, password] = connectionString.split('@')
  
  if (!url || !password) {
    console.log(c.red('❌ URL and password required!'))
    process.exit(1)
  }
  
  // URL'e http:// ekle eğer yoksa
  let fullUrl = url
  if (!url.startsWith('http://') && !url.startsWith('https://')) {
    fullUrl = 'http://' + url
  }
  
  const client = require('../client.js')
  client.start({
    url: fullUrl,
    password: password,
    usecd: usecd
  })
}