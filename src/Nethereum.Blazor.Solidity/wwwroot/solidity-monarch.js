export const solidityLanguage = {
    defaultToken: '',
    tokenPostfix: '.sol',

    keywords: [
        'pragma', 'solidity', 'import', 'as', 'from',
        'contract', 'interface', 'library', 'abstract', 'is',
        'struct', 'enum', 'event', 'error', 'modifier',
        'function', 'constructor', 'fallback', 'receive',
        'if', 'else', 'for', 'while', 'do', 'break', 'continue', 'return', 'returns',
        'try', 'catch', 'throw', 'revert',
        'emit', 'new', 'delete',
        'assembly', 'let', 'switch', 'case', 'default', 'leave',
        'using', 'type', 'mapping',
        'unchecked'
    ],

    typeKeywords: [
        'address', 'bool', 'string', 'bytes',
        'int', 'int8', 'int16', 'int32', 'int64', 'int128', 'int256',
        'uint', 'uint8', 'uint16', 'uint32', 'uint64', 'uint128', 'uint256',
        'bytes1', 'bytes2', 'bytes3', 'bytes4', 'bytes5', 'bytes6', 'bytes7', 'bytes8',
        'bytes9', 'bytes10', 'bytes11', 'bytes12', 'bytes13', 'bytes14', 'bytes15', 'bytes16',
        'bytes17', 'bytes18', 'bytes19', 'bytes20', 'bytes21', 'bytes22', 'bytes23', 'bytes24',
        'bytes25', 'bytes26', 'bytes27', 'bytes28', 'bytes29', 'bytes30', 'bytes31', 'bytes32',
        'fixed', 'ufixed'
    ],

    modifiers: [
        'public', 'private', 'internal', 'external',
        'pure', 'view', 'payable', 'nonpayable',
        'constant', 'immutable',
        'virtual', 'override',
        'storage', 'memory', 'calldata',
        'indexed', 'anonymous'
    ],

    builtinFunctions: [
        'require', 'assert', 'revert',
        'keccak256', 'sha256', 'ripemd160', 'ecrecover',
        'addmod', 'mulmod',
        'selfdestruct',
        'abi', 'encode', 'encodePacked', 'encodeWithSelector', 'encodeWithSignature', 'decode',
        'blockhash', 'gasleft', 'type'
    ],

    constants: [
        'true', 'false',
        'wei', 'gwei', 'ether',
        'seconds', 'minutes', 'hours', 'days', 'weeks'
    ],

    operators: [
        '=', '>', '<', '!', '~', '?', ':',
        '==', '<=', '>=', '!=', '&&', '||', '++', '--',
        '+', '-', '*', '/', '&', '|', '^', '%', '<<',
        '>>', '>>>', '+=', '-=', '*=', '/=', '&=', '|=',
        '^=', '%=', '<<=', '>>=', '>>>='
    ],

    symbols: /[=><!~?:&|+\-*\/\^%]+/,
    escapes: /\\(?:[abfnrtv\\"']|x[0-9A-Fa-f]{1,4}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})/,
    hexdigits: /[0-9a-fA-F]+/,

    tokenizer: {
        root: [
            // NatSpec doc comments
            [/\/\/\/.*$/, 'comment.doc'],
            // Line comments
            [/\/\/.*$/, 'comment'],
            // Block doc comments
            [/\/\*\*/, 'comment.doc', '@doccomment'],
            // Block comments
            [/\/\*/, 'comment', '@comment'],

            // Pragma
            [/pragma\s+solidity/, 'keyword', '@pragma'],

            // Strings
            [/"([^"\\]|\\.)*$/, 'string.invalid'],
            [/'([^'\\]|\\.)*$/, 'string.invalid'],
            [/"/, 'string', '@string_double'],
            [/'/, 'string', '@string_single'],

            // Hex literals
            [/hex"[0-9a-fA-F]*"/, 'number.hex'],
            [/hex'[0-9a-fA-F]*'/, 'number.hex'],

            // Numbers
            [/0[xX]@hexdigits/, 'number.hex'],
            [/\d*\.\d+([eE][\-+]?\d+)?/, 'number.float'],
            [/\d+[eE][\-+]?\d+/, 'number.float'],
            [/\d+/, 'number'],

            // Identifiers and keywords
            [/[a-zA-Z_$][\w$]*/, {
                cases: {
                    '@typeKeywords': 'type',
                    '@keywords': 'keyword',
                    '@modifiers': 'keyword.modifier',
                    '@builtinFunctions': 'predefined',
                    '@constants': 'constant',
                    '@default': 'identifier'
                }
            }],

            // Globals
            [/msg\.(sender|value|data|sig|gas)/, 'variable.predefined'],
            [/block\.(timestamp|number|difficulty|gaslimit|chainid|coinbase|basefee|prevrandao)/, 'variable.predefined'],
            [/tx\.(origin|gasprice)/, 'variable.predefined'],
            [/this/, 'variable.predefined'],
            [/super/, 'variable.predefined'],

            // Delimiters and operators
            [/[{}()\[\]]/, '@brackets'],
            [/[;,.]/, 'delimiter'],
            [/@symbols/, {
                cases: {
                    '@operators': 'operator',
                    '@default': ''
                }
            }],

            // Whitespace
            { include: '@whitespace' }
        ],

        comment: [
            [/[^\/*]+/, 'comment'],
            [/\*\//, 'comment', '@pop'],
            [/[\/*]/, 'comment']
        ],

        doccomment: [
            [/@(param|return|returns|dev|notice|title|author|inheritdoc|custom:\w+)\b/, 'comment.doc.tag'],
            [/[^\/*]+/, 'comment.doc'],
            [/\*\//, 'comment.doc', '@pop'],
            [/[\/*]/, 'comment.doc']
        ],

        pragma: [
            [/;/, 'delimiter', '@pop'],
            [/[\^~>=<]+/, 'operator'],
            [/\d+\.\d+\.\d+/, 'number'],
            [/./, 'keyword']
        ],

        string_double: [
            [/[^\\"]+/, 'string'],
            [/@escapes/, 'string.escape'],
            [/\\./, 'string.escape.invalid'],
            [/"/, 'string', '@pop']
        ],

        string_single: [
            [/[^\\']+/, 'string'],
            [/@escapes/, 'string.escape'],
            [/\\./, 'string.escape.invalid'],
            [/'/, 'string', '@pop']
        ],

        whitespace: [
            [/[ \t\r\n]+/, '']
        ]
    }
};
