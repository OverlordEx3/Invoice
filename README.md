# Invoice
Console tool to generate a proform invoice based on a template.

## Usage:
### Generate an invoice:
`invoice generate`

Note that it will take all the values from the internal dictionary. 
If some key is missing, is going to fail. Instead, use 

`invoice generate --interactive` 

or 

`invoice generate -i`.

### Get a value from dictionary:
`invoice dictionary get [<Key>]`

Where `<key>` can be one or many different keys.

### List values from dictionary:
`invoice dictionary list (--offset <Offset> --limit <Limit>)`

Gets up to `<Limit>` keys retrieved from dictionary starting from `<Offset>`. By default, it lists the first ten keys.

### Set values into the dictionary:
`invoice dictionary set [<Key>=<Value>]`

Sets one or more key/values spplited by '=' into the dictionary.

### Import a file into the dictionary:
`invoice dictionary import <Filename>`

Imports a dictionary file `<Filename>` (one key/value per line, spplited by an equals sign) into the dictionary.

### Version:
`invoice version`

Gets the current version.
